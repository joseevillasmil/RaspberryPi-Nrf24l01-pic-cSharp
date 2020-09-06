using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NRF24L01_raspberry_console
{
    class NrfSlave
    {
        public byte[] Address
        {
            get
            {
                return address;
            }
        }
        public string Name
        {
            get
            {
                return this.Name;
            }
        }
        public bool Status
        {
            get { return this.status; }
        }

        private bool status = false;
        private byte[] address;
        private string name;
        private NRFDriver nrf;

        public NrfSlave(NRFDriver nrf, byte[] address, string name)
        {
            this.address = address;
            this.name = name;
            this.nrf = nrf;
        }
        public async void On()
        {
            await this.RunTransmitter(
                new byte[]{ 0x41, 0x41, 0x41, 0x41,
                            0x41, 0x41, 0x41, 0x41,
                            0x41, 0x41, 0x41, 0x41,
                            0x41, 0x41, 0x41, 0x41 });
        }

        public async void Off()
        {
            await this.RunTransmitter(
                new byte[]{ 0x0, 0x0, 0x0, 0x0,
                            0x0, 0x0, 0x0, 0x0,
                            0x0, 0x0, 0x0, 0x0,
                            0x0, 0x0, 0x0, 0x0 });
        }

        private async Task RunTransmitter(byte[] data)
        {
            this.nrf.Transmit(this.address, data);
            Console.WriteLine($"Status after transmit = 0x{nrf.ReadStatus():X}");
            await Task.Delay(250);
        }

    }
}
