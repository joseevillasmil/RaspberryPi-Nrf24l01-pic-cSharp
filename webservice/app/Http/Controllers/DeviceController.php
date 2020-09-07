<?php

namespace App\Http\Controllers;

use Illuminate\Http\Request;

class DeviceController extends Controller
{
    function command($device, $command) {

        try {

            $cmd = str_replace(['{d}', '{c}'], [$device, $command], env('nrfcmd'));
            exec($cmd);
            return response()->json('ok');
        }catch(\Exception $e) {
            return response()->json('error');
        }

    }
}
