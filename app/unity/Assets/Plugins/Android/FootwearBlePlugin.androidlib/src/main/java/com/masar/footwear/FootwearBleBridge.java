package com.masar.footwear;

import android.Manifest;
import android.app.Activity;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothGatt;
import android.bluetooth.BluetoothGattCallback;
import android.bluetooth.BluetoothGattCharacteristic;
import android.bluetooth.BluetoothGattDescriptor;
import android.bluetooth.BluetoothGattService;
import android.bluetooth.BluetoothManager;
import android.bluetooth.BluetoothProfile;
import android.bluetooth.le.BluetoothLeScanner;
import android.bluetooth.le.ScanCallback;
import android.bluetooth.le.ScanResult;
import android.bluetooth.le.ScanSettings;
import android.content.Context;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;

import java.nio.charset.StandardCharsets;
import java.util.UUID;
import java.util.concurrent.ConcurrentLinkedQueue;

public final class FootwearBleBridge {
    private static final String SHOE_NAME = "ESP32_Shoe";

    private static final UUID SERVICE_UUID = UUID.fromString(
            "6E400001-B5A3-F393-E0A9-E50E24DCCA9E");

    private static final UUID NOTIFY_CHARACTERISTIC_UUID = UUID.fromString(
            "6E400003-B5A3-F393-E0A9-E50E24DCCA9E");

    private static final UUID CLIENT_CONFIGURATION_UUID = UUID.fromString(
            "00002902-0000-1000-8000-00805F9B34FB");

    private static final Handler MAIN_HANDLER = new Handler(Looper.getMainLooper());
    private static final ConcurrentLinkedQueue<String> MESSAGE_QUEUE =
            new ConcurrentLinkedQueue<>();

    private static Activity activity;
    private static BluetoothLeScanner scanner;
    private static BluetoothGatt bluetoothGatt;
    private static boolean scanning;
    private static boolean shouldConnect;

    private FootwearBleBridge() {
    }

    public static void connect(final Activity unityActivity) {
        activity = unityActivity;
        MESSAGE_QUEUE.clear();
        shouldConnect = true;

        MAIN_HANDLER.post(new Runnable() {
            @Override
            public void run() {
                closeGatt();
                startScan();
            }
        });
    }

    public static void disconnect() {
        shouldConnect = false;

        MAIN_HANDLER.post(new Runnable() {
            @Override
            public void run() {
                stopScan();
                closeGatt();
            }
        });
    }

    public static String pollMessage() {
        return MESSAGE_QUEUE.poll();
    }

    private static void startScan() {
        if (!shouldConnect || scanning) {
            return;
        }

        Activity currentActivity = activity;

        if (currentActivity == null) {
            sendStatus("Android activity unavailable");
            return;
        }

        if (!hasRequiredPermissions(currentActivity)) {
            sendStatus("Bluetooth permission missing");
            return;
        }

        BluetoothManager manager =
                (BluetoothManager) currentActivity.getSystemService(
                        Context.BLUETOOTH_SERVICE
                );

        BluetoothAdapter adapter =
                manager == null ? null : manager.getAdapter();

        if (adapter == null) {
            sendStatus("Bluetooth unavailable");
            return;
        }

        if (!adapter.isEnabled()) {
            sendStatus("Turn Bluetooth on");
            return;
        }

        scanner = adapter.getBluetoothLeScanner();

        if (scanner == null) {
            sendStatus("BLE scanner unavailable");
            return;
        }

        ScanSettings settings = new ScanSettings.Builder()
                .setScanMode(ScanSettings.SCAN_MODE_LOW_LATENCY)
                .build();

        scanning = true;
        sendStatus("Searching for ESP32_Shoe...");

        try {
            scanner.startScan(null, settings, scanCallback);
        } catch (SecurityException exception) {
            scanning = false;
            sendStatus("Bluetooth permission denied");
            return;
        }

        MAIN_HANDLER.postDelayed(new Runnable() {
            @Override
            public void run() {
                if (scanning) {
                    stopScan();
                    sendStatus("ESP32_Shoe not found - retrying");
                    scheduleScanRetry();
                }
            }
        }, 12000L);
    }

    private static final ScanCallback scanCallback = new ScanCallback() {
        @Override
        public void onScanResult(int callbackType, ScanResult result) {
            if (!shouldConnect || result == null || result.getDevice() == null) {
                return;
            }

            BluetoothDevice device = result.getDevice();
            String advertisedName = null;

            if (result.getScanRecord() != null) {
                advertisedName = result.getScanRecord().getDeviceName();
            }

            if (advertisedName == null) {
                try {
                    advertisedName = device.getName();
                } catch (SecurityException ignored) {
                }
            }

            if (!SHOE_NAME.equals(advertisedName)) {
                return;
            }

            stopScan();
            connectGatt(device);
        }

        @Override
        public void onScanFailed(int errorCode) {
            scanning = false;
            sendStatus("BLE scan failed: " + errorCode);
            scheduleScanRetry();
        }
    };

    private static void connectGatt(BluetoothDevice device) {
        Activity currentActivity = activity;

        if (currentActivity == null || !shouldConnect) {
            return;
        }

        sendStatus("Connecting to ESP32_Shoe...");

        try {
            bluetoothGatt = device.connectGatt(
                    currentActivity,
                    false,
                    gattCallback,
                    BluetoothDevice.TRANSPORT_LE
            );
        } catch (SecurityException exception) {
            sendStatus("Bluetooth permission denied");
        }
    }

    private static final BluetoothGattCallback gattCallback =
            new BluetoothGattCallback() {
                @Override
                public void onConnectionStateChange(
                        BluetoothGatt gatt,
                        int status,
                        int newState) {
                    if (newState == BluetoothProfile.STATE_CONNECTED) {
                        sendStatus("Discovering footwear...");

                        try {
                            gatt.discoverServices();
                        } catch (SecurityException exception) {
                            sendStatus("Bluetooth permission denied");
                        }
                    } else if (newState == BluetoothProfile.STATE_DISCONNECTED) {
                        if (bluetoothGatt == gatt) {
                            closeGatt();
                        } else {
                            try {
                                gatt.close();
                            } catch (Exception ignored) {
                            }
                        }

                        if (shouldConnect) {
                            sendStatus("Footwear disconnected - retrying");
                            scheduleScanRetry();
                        }
                    }
                }

                @Override
                public void onServicesDiscovered(BluetoothGatt gatt, int status) {
                    if (status != BluetoothGatt.GATT_SUCCESS) {
                        sendStatus("Could not discover footwear services");
                        return;
                    }

                    BluetoothGattService service = gatt.getService(SERVICE_UUID);

                    if (service == null) {
                        sendStatus("Footwear service not found");
                        return;
                    }

                    BluetoothGattCharacteristic characteristic =
                            service.getCharacteristic(NOTIFY_CHARACTERISTIC_UUID);

                    if (characteristic == null) {
                        sendStatus("Footwear characteristic not found");
                        return;
                    }

                    try {
                        boolean enabled =
                                gatt.setCharacteristicNotification(characteristic, true);

                        if (!enabled) {
                            sendStatus("Could not enable step notifications");
                            return;
                        }

                        BluetoothGattDescriptor descriptor =
                                characteristic.getDescriptor(CLIENT_CONFIGURATION_UUID);

                        if (descriptor != null) {
                            descriptor.setValue(
                                    BluetoothGattDescriptor.ENABLE_NOTIFICATION_VALUE
                            );
                            gatt.writeDescriptor(descriptor);
                        }

                        sendStatus("Connected");
                    } catch (SecurityException exception) {
                        sendStatus("Bluetooth permission denied");
                    }
                }

                @Override
                public void onCharacteristicChanged(
                        BluetoothGatt gatt,
                        BluetoothGattCharacteristic characteristic) {
                    if (!NOTIFY_CHARACTERISTIC_UUID.equals(characteristic.getUuid())) {
                        return;
                    }

                    byte[] value = characteristic.getValue();

                    if (value == null || value.length == 0) {
                        return;
                    }

                    String message =
                            new String(value, StandardCharsets.UTF_8).trim();

                    if (!message.isEmpty()) {
                        sendToUnity(message);
                    }
                }
            };

    private static void scheduleScanRetry() {
        if (!shouldConnect) {
            return;
        }

        MAIN_HANDLER.postDelayed(new Runnable() {
            @Override
            public void run() {
                if (shouldConnect && bluetoothGatt == null) {
                    startScan();
                }
            }
        }, 3000L);
    }

    private static void stopScan() {
        if (!scanning || scanner == null) {
            scanning = false;
            return;
        }

        try {
            scanner.stopScan(scanCallback);
        } catch (SecurityException ignored) {
        }

        scanning = false;
    }

    private static void closeGatt() {
        BluetoothGatt gatt = bluetoothGatt;
        bluetoothGatt = null;

        if (gatt == null) {
            return;
        }

        try {
            gatt.disconnect();
        } catch (Exception ignored) {
        }

        try {
            gatt.close();
        } catch (Exception ignored) {
        }
    }

    private static boolean hasRequiredPermissions(Activity activity) {
        if (Build.VERSION.SDK_INT >= 31) {
            return activity.checkSelfPermission(Manifest.permission.BLUETOOTH_SCAN)
                    == PackageManager.PERMISSION_GRANTED
                    && activity.checkSelfPermission(Manifest.permission.BLUETOOTH_CONNECT)
                    == PackageManager.PERMISSION_GRANTED;
        }

        if (Build.VERSION.SDK_INT >= 23) {
            return activity.checkSelfPermission(Manifest.permission.ACCESS_FINE_LOCATION)
                    == PackageManager.PERMISSION_GRANTED;
        }

        return true;
    }

    private static void sendStatus(String status) {
        sendToUnity("STATUS:" + status);
    }

    private static void sendToUnity(final String message) {
        if (message == null || message.isEmpty()) {
            return;
        }

        MESSAGE_QUEUE.offer(message);
    }
}
