import { Component } from '@angular/core';
import {BluetoothSerial} from "@ionic-native/bluetooth-serial/ngx";
import {HttpClient} from "@angular/common/http";

@Component({
  selector: 'app-home',
  templateUrl: 'home.page.html',
  styleUrls: ['home.page.scss'],
})
export class HomePage {

  constructor(private http: HttpClient) {}

  command(device, command) {
      this.http.get('http://192.168.1.150:8080/nrf/api/' + device + '/' + command).subscribe(
          (data) => {
            alert('ok');
          }, () => {alert('error')}
      );
  }

}
