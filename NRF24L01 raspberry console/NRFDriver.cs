using System;
using System.Linq;
using System.Threading;
using Unosquare.RaspberryIO.Abstractions;
using Unosquare.WiringPi;

namespace NRF24L01_raspberry_console
{
    class NRFDriver
    {
        private readonly ISpiChannel spi;
        private readonly IGpioPin cePin;
        private readonly IGpioPin irqPin;
        private readonly AutoResetEvent irqEvent;
        private ISpiChannel channel0;
        private object chipEnable;
        private object irq;
        private const byte StatusPipeShift = 1;
        private const byte StatusPipeMask = 0b111 << StatusPipeShift;

        public NRFDriver(ISpiChannel spi, IGpioPin cePin, IGpioPin irqPin)
        {
            this.spi = spi;
            this.cePin = cePin;
            this.irqPin = irqPin;
            this.cePin.PinMode = Unosquare.RaspberryIO.Abstractions.GpioPinDriveMode.Output;
            this.cePin.Write(false);

            this.irqPin.PinMode = Unosquare.RaspberryIO.Abstractions.GpioPinDriveMode.Input;
            this.irqPin.InputPullMode = Unosquare.RaspberryIO.Abstractions.GpioPinResistorPullMode.PullUp;

            this.irqEvent = new AutoResetEvent(false);
            this.irqPin.RegisterInterruptCallback(Unosquare.RaspberryIO.Abstractions.EdgeDetection.FallingEdge, () => this.irqEvent.Set());
        }


        public byte ReadStatus()
        {
            return ReadRegister(Register.STATUS, 1)[0];
        }

        public byte ReadFIFOStatus() => ReadRegister(Register.FIFO_STATUS, 1)[0];

        public byte ObserveTX()
        {
            return ReadRegister(Register.OBSERVE_TX, 1)[0];
        }

        public void EnableReceiving()
        {
            this.cePin.Write(true);
        }

        public void DisableReceiving()
        {
            this.cePin.Write(false);
        }

        public byte Transmit(byte[] address, byte[] data)
        {
            ConfigRegister originalConfig;
            {
                var config = originalConfig = (ConfigRegister)ReadRegister(Register.CONFIG, 1)[0];
                config = config & ~ConfigRegister.PRIM_RX;
                WriteRegister(Register.CONFIG, (byte)config);
            }

            // write TX addr
            WriteRegister(Register.TX_ADDR, address);
            WriteRegister(Register.RX_ADDR_P0, address);

            // write TX pld
            SendCommand(Command.WriteTXPayload, data);

            // CE high
            this.cePin.Write(true);
            // wait for transmission finish
            this.irqPin.WaitForValue(Unosquare.RaspberryIO.Abstractions.GpioPinValue.Low, 10000);
            // CE low
            this.cePin.Write(false);

            WriteRegister(Register.CONFIG, (byte)originalConfig);

            var st = ReadStatus();
            WriteRegister(Register.STATUS, (1 << 5) | (1 << 4));

            return st;
        }

        public (int pipe, byte[] data)? ReceiveFrame()
        {
            var currentFrame = ReadRXFrame();

            if (currentFrame != null)
            {
                return currentFrame;
            }

            WriteRegister(Register.STATUS, 1 << 6);

            this.irqEvent.WaitOne();

            return ReadRXFrame();
        }

        private (int pipe, byte[] data)? ReadRXFrame()
        {
            var fifo = ReadFIFOStatus();

            if ((fifo & (1 << 0)) == 1)
            {
                return null;
            }

            var st = ReadStatus();

            var filledPipe = (st & StatusPipeMask) >> StatusPipeShift;

            var data = SendCommandWithResponse(Command.R_RX_PAYLOAD, 32);

            WriteRegister(Register.STATUS, 1 << 6);

            return (filledPipe, data);
        }

        internal void EnableCarrier()
        {
            WriteRegister(Register.RF_SETUP, (1 << 7) | (1 << 4));

            // CE high
            this.cePin.Write(true);
            // sleep
            Thread.Sleep(10000);
            // CE low
            this.cePin.Write(false);
        }

        public void Flush()
        {
            SendCommand(Command.FlushTX);
            SendCommand(Command.FlushRX);
            var s = ReadStatus();
            WriteRegister(Register.STATUS, s);
        }

        // copied from nrf.c of the PIC C library.
        // IT's important to have the same configuration.
        public void configure_tx()
        {
            spi.Write(new byte[] { 0x21, 0x01 }); // write auto-ack
            spi.Write(new byte[] { 0x22, 0x01 });
            spi.Write(new byte[] { 0x23, 0x03 });
            //-----------
            spi.Write(new byte[] { 0x24, 0xFF });
            spi.Write(new byte[] { 0x26, 0x07 });
            //-----------
            spi.Write(new byte[] { 0x31, 16 });
            //-----------
            spi.Write(new byte[] { 0x25, 0x02 });
            //-----------
            spi.Write(new byte[] { 0x27, 0xBF });

        }

        // copied from nrf.c of the PIC C library.
        // IT's important to have the same configuration.
        public void configure_rx(byte[] rx_address)
        {
            spi.Write(new byte[] { 0x20, 0x01 });
            spi.Write(new byte[] { 0x21, 0x01 }); // write auto-ack
            spi.Write(new byte[] { 0x22, 0x01 });
            spi.Write(new byte[] { 0x23, 0x03 });
            //-----------
            spi.Write(new byte[] { 0x26, 0x07 });
            //-----------
            spi.Write(new byte[] { 0x31, 16 });
            //-----------
            spi.Write(new byte[] { 0x25, 0x02 });
            spi.Write(new byte[] { 0x25, 0x02 });
            //-----------
            spi.Write(new byte[] { 0x2A, rx_address[0], rx_address[1], rx_address[2], rx_address[3], rx_address[4] });
            spi.Write(new byte[] { 0x20, 0x3B });
            spi.Write(new byte[] { 0x27, 0xCF });

        }
        public void Setup()
        {
            // Powered up, 2-byte CRC, primary receiver
            WriteRegister(Register.CONFIG, (byte)(ConfigRegister.PWR_UP | ConfigRegister.EN_CRC | ConfigRegister.CRC0 | ConfigRegister.PRIM_RX));
            // enable 1 data pipe
            WriteRegister(Register.EN_AA, 1 << 0);
            // enable 1 data pipe
            WriteRegister(Register.EN_RXADDR, 1 << 0);
            // 3 bytes address
            WriteRegister(Register.SETUP_AW, 0b01);
            // 4ms delay between retransmission (upper half), 15 retransmits (lower half)
            WriteRegister(Register.SETUP_RETR, (0b1111 << 4) | (0b1111 << 4));
            // 1 RF channel (2401MHz)
            WriteRegister(Register.RF_CH, 1);
            // 250kbps bit rate, lowest RF output power (-18dBm, ~0.02mW)
            WriteRegister(Register.RF_SETUP, (byte)(RFSetupRegister.RF_DR_LOW | RFSetupRegister.RF_PWR0));
        }

        public void EnableRX(int pipe, byte[] address)
        {
            // EN_AA
            var aa = ReadRegister(Register.EN_AA, 1)[0];
            aa |= (byte)(1 << pipe);
            WriteRegister(Register.EN_AA, aa);

            // EN_RXADDR
            var rxaddr = ReadRegister(Register.EN_RXADDR, 1)[0];
            rxaddr |= (byte)(1 << pipe);
            WriteRegister(Register.EN_RXADDR, rxaddr);

            // RX_ADDR_Px
            if (pipe == 1)
            {
                // full address for pipes 0 and 1
                WriteRegister((Register)((byte)Register.RX_ADDR_P0 + pipe), address);
            }
            else
            {
                WriteRegister((Register)((byte)Register.RX_ADDR_P0 + pipe), address.First());
            }

            WriteRegister((Register)((byte)Register.RX_PW_P0 + pipe), 32);
        }

        public void PowerDown()
        {
            WriteRegister(Register.CONFIG, 0);
        }

        public void PowerUp()
        {
            WriteRegister(Register.CONFIG, (byte)ConfigRegister.PWR_UP);
        }

        private byte[] ReadRegister(Register register, int size)
        {
            var command = new byte[1 + size];
            command[0] = (byte)register;

            var result = spi.SendReceive(command);

            return result.Skip(1).ToArray();
        }

        private void WriteRegister(Register register, params byte[] data)
        {
            var command = new byte[1 + data.Length];
            command[0] = (byte)(0b00100000 | (byte)register);
            data.CopyTo(command, 1);

            spi.Write(command);
        }

        private void SendCommand(Command cmd, params byte[] data)
        {
            var command = new byte[1 + data.Length];
            command[0] = (byte)cmd;
            data.CopyTo(command, 1);

            spi.Write(command);
        }

        private byte[] SendCommandWithResponse(Command cmd, int responseSize, params byte[] data)
        {
            var command = new byte[1 + Math.Max(data.Length, responseSize)];
            command[0] = (byte)cmd;
            data.CopyTo(command, 1);

            var response = spi.SendReceive(command);

            return response.Skip(1).ToArray();
        }


        private enum Register : byte
        {
            STATUS = 0x07,
            TX_ADDR = 0x10,
            CONFIG = 0x00,
            RF_CH = 0x05,
            RF_SETUP = 0x6,
            SETUP_AW = 0x3,
            EN_AA = 0x1,
            SETUP_RETR = 0x4,
            EN_RXADDR = 0x2,
            RX_ADDR_P0 = 0x0A,
            OBSERVE_TX = 0x8,
            RX_PW_P0 = 0x11,
            FIFO_STATUS = 0x17
        }

        private enum Command : byte
        {
            WriteTXPayload = 0b10100000,
            R_RX_PAYLOAD = 0b01100001,
            FlushTX = 0b11100001,
            FlushRX = 0b11100010,
        }

        [Flags]
        private enum ConfigRegister : byte
        {
            PRIM_RX = 1 << 0,
            PWR_UP = 1 << 1,
            CRC0 = 1 << 2,
            EN_CRC = 1 << 3,
        }

        [Flags]
        private enum RFSetupRegister : byte
        {
            RF_DR_LOW = 1 << 5,
            RF_PWR0 = 1 << 1,
            RF_PWR1 = 1 << 2,
        }
    }
}
