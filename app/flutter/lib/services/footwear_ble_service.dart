import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_blue_plus/flutter_blue_plus.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:shared_preferences/shared_preferences.dart';

class FootwearBleService extends ChangeNotifier {
  static final Guid _serviceUuid =
      Guid('6E400001-B5A3-F393-E0A9-E50E24DCCA9E');

  static final Guid _characteristicUuid =
      Guid('6E400003-B5A3-F393-E0A9-E50E24DCCA9E');

  BluetoothDevice? _device;
  BluetoothCharacteristic? _characteristic;
  static const String _savedShoeKey = 'masar_saved_shoe_id';

  StreamSubscription<List<ScanResult>>? _scanSubscription;
  StreamSubscription<List<int>>? _valueSubscription;
  StreamSubscription<BluetoothConnectionState>? _connectionSubscription;
  Timer? _reconnectTimer;
  bool _isConnecting = false;
  bool _manualDisconnect = false;
  bool _disposed = false;

bool get isConnecting => _isConnecting;

  String _status = 'Disconnected';

  String get status => _status;

  // MasAR+ will set this later.
  // Every valid "Pressed" message will call this function.
  VoidCallback? onStep;
  VoidCallback? onPower;

  Future<void> connect() async {
    if (_isConnecting || _status == 'Connected') return;

    _isConnecting = true;
    _manualDisconnect = false;
    _reconnectTimer?.cancel();
    try {
      _setStatus('Requesting permission...');

      final permissions = await [
        Permission.bluetoothScan,
        Permission.bluetoothConnect,
      ].request();

      final scanGranted =
          permissions[Permission.bluetoothScan]?.isGranted ?? false;

      final connectGranted =
          permissions[Permission.bluetoothConnect]?.isGranted ?? false;

      if (!scanGranted || !connectGranted) {
        _setStatus('Bluetooth permission denied');
        return;
      }

      // Make sure Bluetooth is actually available/on.
      final adapterState = await FlutterBluePlus.adapterState
          .where((state) => state != BluetoothAdapterState.unknown)
          .first;

      if (adapterState != BluetoothAdapterState.on) {
        _setStatus('Turn Bluetooth on');
        return;
      }

      _setStatus('Searching for ESP32_Shoe...');

      await _scanSubscription?.cancel();

      bool found = false;

      _scanSubscription = FlutterBluePlus.scanResults.listen(
        (results) async {
          if (found) return;

          for (final result in results) {
            final name = result.advertisementData.advName;

            if (name == 'ESP32_Shoe') {
              found = true;

              await FlutterBluePlus.stopScan();
              await _scanSubscription?.cancel();
              _scanSubscription = null;

              _device = result.device;

              await _connectToDevice(result.device);

              break;
            }
          }
        },
      );

      await FlutterBluePlus.startScan(
        timeout: const Duration(seconds: 10),
      );

      await FlutterBluePlus.isScanning
          .where((scanning) => scanning == false)
          .first;

      if (!found) {
        _setStatus('ESP32_Shoe not found');
      }
    } catch (e) {
      debugPrint('Footwear BLE error: $e');
      _setStatus('Connection failed');
    }
    finally {
      _isConnecting = false;
      if (!_disposed) notifyListeners();
    }
  }

  Future<void> _connectToDevice(BluetoothDevice device) async {
    try {
      _setStatus('Connecting...');

      await _connectionSubscription?.cancel();

      _connectionSubscription = device.connectionState.listen((state) {
        if (state == BluetoothConnectionState.disconnected) {
          if (!_manualDisconnect && _status == 'Connected') {
            _scheduleReconnect();
          } else if (!_manualDisconnect) {
            _setStatus('Disconnected');
          }
        }
      });

      await device.connect(
  timeout: const Duration(seconds: 10),
  license: License.free,
);

      _setStatus('Discovering footwear...');

      final services = await device.discoverServices();

      BluetoothCharacteristic? target;

      for (final service in services) {
        if (service.uuid == _serviceUuid) {
          for (final characteristic in service.characteristics) {
            if (characteristic.uuid == _characteristicUuid) {
              target = characteristic;
              break;
            }
          }
        }
      }

      if (target == null) {
        _setStatus('Footwear characteristic not found');
        return;
      }

      _characteristic = target;

      await _valueSubscription?.cancel();

      _valueSubscription =
          target.onValueReceived.listen(_handleMessage);

      await target.setNotifyValue(true);

      // Remember this shoe only after a successful connection.
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(_savedShoeKey, device.remoteId.str);

      _setStatus('Connected');

      debugPrint('ESP32_Shoe connected');
    } catch (e) {
      debugPrint('ESP32 connection error: $e');
      _setStatus('Connection failed');
    }
  }

  void _handleMessage(List<int> value) {
  final message = utf8.decode(value).trim();

  debugPrint('ESP32 MESSAGE: $message');

  if (message.toLowerCase() == 'pressed') {
    // If we received data, BLE is definitely connected.
    if (_status != 'Connected') {
      _setStatus('Connected');
    }

    debugPrint('STEP DETECTED');
    onStep?.call();

  } else if (message.toLowerCase() == 'power') {
    // If we received data, BLE is definitely connected.
    if (_status != 'Connected') {
      _setStatus('Connected');
    }

    debugPrint('POWER DETECTED');
    onPower?.call();
  }
}

  void _setStatus(String newStatus) {
    _status = newStatus;
    notifyListeners();

    debugPrint('Footwear: $newStatus');
  }
  
  Future<void> reconnectSavedShoe() async {
   final prefs = await SharedPreferences.getInstance();
   final savedId = prefs.getString(_savedShoeKey);

    // First-time users must connect manually.
    if (savedId == null || savedId.isEmpty) return;

    _setStatus('Reconnecting to saved shoe...');

    try {
     final permissions = await [
       Permission.bluetoothScan,
       Permission.bluetoothConnect,
     ].request();

      if (permissions[Permission.bluetoothScan]?.isGranted != true ||
          permissions[Permission.bluetoothConnect]?.isGranted != true) {
        _setStatus('Bluetooth permission denied');
        return;
      }

      final adapterState = await FlutterBluePlus.adapterState
          .where((state) => state != BluetoothAdapterState.unknown)
          .first;

      if (adapterState != BluetoothAdapterState.on) {
        _setStatus('Turn Bluetooth on');
        return;
      }

      // Reconnect using the saved identifier.
      final device = BluetoothDevice.fromId(savedId);
      _device = device;

      await _connectToDevice(device);
   } catch (e) {
      debugPrint('Automatic shoe reconnect failed: $e');
      _setStatus('Saved shoe unavailable');
    }
  }

  
  void _scheduleReconnect() {
    if (_manualDisconnect || _disposed || _reconnectTimer != null) {
      return;
    }

    _setStatus('Disconnected - retrying...');

    _reconnectTimer = Timer(
      const Duration(seconds: 5),
      () async {
        _reconnectTimer = null;

        if (_manualDisconnect || _disposed || _isConnecting) {
          return;
        }

        await reconnectSavedShoe();

        if (_status != 'Connected' && !_manualDisconnect && !_disposed) {
          _scheduleReconnect();
        }
      },
    );
  }

  Future<void> disconnect() async {
    _manualDisconnect = true;
    _reconnectTimer?.cancel();
    _reconnectTimer = null;
    await FlutterBluePlus.stopScan();

    await _scanSubscription?.cancel();
    _scanSubscription = null;

    await _valueSubscription?.cancel();
    _valueSubscription = null;

    await _connectionSubscription?.cancel();
    _connectionSubscription = null;

    await _device?.disconnect();

    _device = null;
    _characteristic = null;

    _setStatus('Disconnected');
  }
  
  Future<void> forgetShoe() async {
    // Disconnect the currently connected shoe.
    await disconnect();

    // Delete the remembered shoe identifier.
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_savedShoeKey);

    _setStatus('Shoe forgotten');
  }

  @override
  void dispose() {
    _disposed = true;
    _manualDisconnect = true;
    _reconnectTimer?.cancel();
    _reconnectTimer = null;
    _scanSubscription?.cancel();
    _valueSubscription?.cancel();
    _connectionSubscription?.cancel();

    super.dispose();
  }
}