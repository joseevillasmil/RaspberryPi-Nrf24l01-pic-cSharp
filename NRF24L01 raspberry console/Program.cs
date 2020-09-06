using System;
using System.Collections.Generic;
using System.Threading;
using Unosquare.RaspberryIO;
using Unosquare.WiringPi;

namespace NRF24L01_raspberry_console
{
    class Program
    {
        private static List<NrfSlave> RECEPTORS = new List<NrfSlave>();
        static void Main(string[] args)
        {
            // Inicializamos el GPIO de la raspberry
            Pi.Init<BootstrapWiringPi>();

            // Colocamos una frecuencia baja, para testing.
            Pi.Spi.Channel0Frequency = 9600;
            
            // Inicializamos el driver para el NRF24l01
            NRFDriver nrf = new NRFDriver(Pi.Spi.Channel0, Pi.Gpio[06], Pi.Gpio[05]);

            nrf.PowerDown();

            Thread.Sleep(100);

            nrf.PowerUp();

            Thread.Sleep(100);

            nrf.configure_tx();
            // Agregamos a la lista cada Slave = exclavo o receptor.
            RECEPTORS.Add(
                    new NrfSlave(nrf, new byte[] { 0x31, 0xE4, 0xE4, 0xE4, 0xE4 }, "Encendedor 1")
                );
            RECEPTORS.Add(
                    new NrfSlave(nrf, new byte[] { 0x32, 0xE4, 0xE4, 0xE4, 0xE4 }, "Encendedor 1")
                );

            if (args.Length > 0 && !String.IsNullOrEmpty(args[0]) && !String.IsNullOrEmpty(args[1]))
            {
                // validamos el indice de entrada
                int i;
                if (!int.TryParse(args[0], out i) || i == 0 || i > 2)
                {
                    Console.WriteLine("Indice no válido");
                    Environment.Exit(0);
                }

                switch (args[1])
                {
                
                    case "on":
                        Console.WriteLine("Encendiendo...");
                        RECEPTORS[(i -1)].On();
                        break;

                    case "off":
                        Console.WriteLine("Apagando...");
                        RECEPTORS[(i - 1)].Off();
                        break;

                    default:
                        Console.WriteLine("Comando no válido");
                        break;
                }
            }
        }
    }
}
