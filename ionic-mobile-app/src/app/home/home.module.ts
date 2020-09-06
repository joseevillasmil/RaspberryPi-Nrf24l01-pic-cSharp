import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular';
import { FormsModule } from '@angular/forms';
import { HomePage } from './home.page';
import { BluetoothSerial } from '@ionic-native/bluetooth-serial/ngx';


import { HomePageRoutingModule } from './home-routing.module';
import {HttpClient, HttpClientModule} from "@angular/common/http";


@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    IonicModule,
    HomePageRoutingModule,
  ],
  declarations: [HomePage],
  providers: [
      HttpClient
  ]
})
export class HomePageModule {}
