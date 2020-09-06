//nrf.c
// V1.0 05-05-2016

// a piece of driver i wrote for NRF which is working great with auto ack
// just finish off step1, 2 and 3 and go with example main.. this works

// FYI only one pipe used rest up to you

// step1 user preference------------
//---------------- declare slave address----------------
#define s_ad1 0xE4 // this is the remote station, so called slave
#define s_ad2 0xE4 // this is the remote station, so called slave
#define s_ad3 0xE4 // this is the remote station, so called slave
#define s_ad4 0xE4 // this is the remote station, so called slave
#define s_ad5 0xE4 // this is the remote station, so called slave
//---------------- declare slave address----------------

// step2 user preference------------
// --------------- define master addresses -----------------------
#define master_add1 0xE1 // this is the base station
#define master_add2 0xE2 // this is the base station
#define master_add3 0xE3 // this is the base station
#define master_add4 0xE4 // this is the base station
#define master_add5 0xE5 // this is the base station
// --------------- define master addresses -----------------------

// step3 user preference------------
#define PAY_LOAD_BYTES 16 // number in INTeger max 32 // number of bytes to transfer through RF


static byte RF_RCV_DATA[PAY_LOAD_BYTES]; // RF_RCV_DATA this array holds the data received wirelessly
static byte RF_TX_DATA[PAY_LOAD_BYTES]; //  RF_TX_DATA you will push the data into it to send in wireless

//-------------------------------------------------------------
#define W_REGISTER 0x20
#define R_RX_PAYLOAD 0x61
#define W_TX_PAYLOAD 0xa0
//********** DEFINE PORT NAMES

#define RF24_xfer(xdata)   bb_xfer(xdata)  //Send/receive data through SPI
#define RTX_CSN_Low()      output_low(RF24_CS)        //Controls bit Chipselect
#define RTX_CSN_High()     output_high(RF24_CS)       //Controls bit Chipselect
#define RTX_CE_Low()       output_low(RF24_CE)       //Controls bit Chipenable
#define RTX_CE_High()      output_high(RF24_CE)        //Controls bit Chipenable

//--------------------------------------------------------------
//--------------------------------------------------------------
   int1 DATA_IN_RX = 0;
//--------------------------------------------------------------
#include <STDLIB.H>
/*******************************************************************************/


void pulse_CSN()
{
    RTX_CSN_High();;
    delay_us(20);
    RTX_CSN_Low();
}

void init_rf(){
    RTX_CE_Low();
    RTX_CSN_High();
}

void flush_rx(){ // write it at uC startup
    pulse_CSN();
    //-----------
    SPI_XFER(0xe2); //Flush RX FIFO if it is not flushed during start up then NRF will not receive anymore data
    pulse_CSN();
}

void configure_tx(){

    pulse_CSN();
    SPI_XFER(0x21); // write auto-ack
    SPI_XFER(0x01);
    pulse_CSN();
    //-----------
   
    SPI_XFER(0x22); // write enable pipes total 1
    SPI_XFER(0x01);   
    pulse_CSN();
   
    SPI_XFER(0x23); //address width = 5 bytes
    SPI_XFER(0x03);
    pulse_CSN();
    //-----------
   
    SPI_XFER(0x24); // write re-tx delay = 4ms + 15 times retransmit
    SPI_XFER(0xFF);
    pulse_CSN();   
   
    SPI_XFER(0x26); //data rate = 1MB
    SPI_XFER(0x07);
    pulse_CSN();
    //-----------
    SPI_XFER(0x31);  //x byte payload defined above
    SPI_XFER(PAY_LOAD_BYTES);
    pulse_CSN();
   
    //-----------
    SPI_XFER(0x25); //set channel 2
    SPI_XFER(0x02);
   
    pulse_CSN();
   
    //-----------
    SPI_XFER(0x27); //reset all tx related interrupts
    SPI_XFER(0xBF);
    RTX_CSN_High();

}

int1 MAX_RT(){
   
   int temp_fifo_register = 0;
   pulse_csn();
   temp_fifo_register = SPI_XFER(0);
   RTX_CSN_high();

   if(bit_test(temp_fifo_register,4)){
      return(1);
   }
   else
   {   
      return(0);
   }

}

int1 rf_data_sent(){

   int temp_fifo_register = 0;
   pulse_csn();
   temp_fifo_register = SPI_XFER(0);
   RTX_CSN_high();

   if(bit_test(temp_fifo_register,5)){
      return(1);
   }
   else
   {   
      return(0);
   }
}

int send_shock_burst(byte rf_x1, byte rf_x2, byte rf_x3, byte rf_x4, byte rf_x5){ // takes total 200 ms approx
   
    int nrf_i;

    configure_tx();
   
    RTX_CE_Low();
    pulse_CSN();   
    SPI_XFER(W_REGISTER); //PTX, CRC enabled
    SPI_XFER(0x3A);
    pulse_CSN();

    //-----------
    SPI_XFER(W_TX_PAYLOAD);
    for(nrf_i=0;nrf_i<PAY_LOAD_BYTES;nrf_i++)
        {
            SPI_XFER(RF_TX_DATA[nrf_i]); //clock in payload
            //printf("%i, %c",i,RF_TX_DATA[i]);
        }

    pulse_CSN();
    //-----------
    SPI_XFER(0x30); // TX address
      SPI_XFER(rf_x1);
      SPI_XFER(rf_x2);
      SPI_XFER(rf_x3);
      SPI_XFER(rf_x4);
      SPI_XFER(rf_x5);
    pulse_CSN();

    SPI_XFER(0x2A); // Pipe 0 address
      SPI_XFER(rf_x1);
      SPI_XFER(rf_x2);
      SPI_XFER(rf_x3);
      SPI_XFER(rf_x4);
      SPI_XFER(rf_x5);
    pulse_CSN();

    //----------------
    RTX_CE_High();
    delay_us(50);
    RTX_CE_low();
   
   delay_ms(150); // just a safe delay to utilise max retransmitts
   
   if(rf_data_sent()){
      return(1);
   }
   else
   {
      if(MAX_RT()){
         return(0);
      }
      else
      {
         return(2);
      }
   }
}

void rf_read_Data()
{
    int rf_i;
    RTX_CSN_Low();

    spi_xfer(R_RX_PAYLOAD); //Read RX payload
    for(rf_i=0;rf_i<PAY_LOAD_BYTES;rf_i++)
    {   
        RF_RCV_DATA[rf_i] = spi_xfer(0x00);
        // printf("%c",RF_RCV_DATA[rf_i]);
    }
    //printf("\n");
    pulse_CSN();
   
    spi_xfer(0xe2); //Flush RX FIFO
    pulse_CSN();
    //-----------
    spi_xfer(0x27); //reset all rx related ints
    spi_xfer(0xCF);
    RTX_CSN_High();
}

void configure_RX(byte rf_slave_addr1, byte rf_slave_addr2, byte rf_slave_addr3, byte rf_slave_addr4, byte rf_slave_addr5)
{
    int i_rf_rx;
    RTX_CE_Low();;
    RTX_CSN_Low();
    spi_xfer(W_REGISTER); //PRX, CRC enabled
    spi_xfer(0x3B);
    pulse_CSN();
    delay_ms(2);
    //-----------
    spi_xfer(0x21); // write auto-ack
    spi_xfer(0x01);
    pulse_CSN();
    //-----------
    spi_xfer(0x22); // write enable pipes total 1
    spi_xfer(0x01);   
    pulse_CSN();
    //-----------
    spi_xfer(0x23); //address width = 5 bytes
    spi_xfer(0x03);
    pulse_CSN();
    //-----------
    spi_xfer(0x26); //data rate = 1MB
    spi_xfer(0x07);
    pulse_CSN();
    //-----------
    spi_xfer(0x31);  //4 byte payload
    spi_xfer(PAY_LOAD_BYTES);
    pulse_CSN();
    //-----------
    spi_xfer(0x25); //set channel 2
    spi_xfer(0x02);
    pulse_CSN();

    //----------------
    spi_xfer(0x2A); //set address E7E7E7E7E7
      spi_xfer(rf_slave_addr1);
      spi_xfer(rf_slave_addr2);   
      spi_xfer(rf_slave_addr3);
      spi_xfer(rf_slave_addr4);
      spi_xfer(rf_slave_addr5);     
    pulse_CSN();

    //----------------
    spi_xfer(W_REGISTER); //PWR_UP = 1
    spi_xfer(0x3B);

    pulse_CSN();
    //-----------
    spi_xfer(0x27); //reset all rx related ints
    spi_xfer(0xCF);
    RTX_CSN_High();
    RTX_CE_High();
}

int1 data_in_rf(){

   int temp_fifo_register = 0;
   
   pulse_csn();
   spi_xfer(0x17);
   temp_fifo_register = spi_xfer(0);
   pulse_csn();   

      spi_xfer(0x27); // clear all rx related INTS
      spi_xfer(0xCF);
      RTX_CSN_high();

   if(bit_test(temp_fifo_register,0)){
      return(0);
   }
   else
   {   
      return(1);
   }
}
