import 'package:camera/camera.dart';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'package:provider/provider.dart';
 
import '../services/footwear_ble_service.dart';

import '../services/step_counter_service.dart';
import '../services/unity_hunt_bridge.dart';
 
class HomeScreen extends StatefulWidget {

  final bool active;
 
  const HomeScreen({

    super.key,

    this.active = true,

  });
 
  @override

  State<HomeScreen> createState() => _HomeScreenState();

}
 
class _HomeScreenState extends State<HomeScreen> with WidgetsBindingObserver {

  static const accent = Color(0xFF722F42);
  static const background = Color(0xFFF8F5EF);
 
  CameraController? _camera;

  Future<void>? _cameraOperation;

  bool _launching = false;

  bool _appResumed = true;

  String? _cameraMessage;

  FootwearBleService? _footwear;
 
  bool get _shouldUseCamera =>

      mounted && widget.active && _appResumed && !_launching;
 
  @override

  void initState() {

  super.initState();

  WidgetsBinding.instance.addObserver(this);
 
  WidgetsBinding.instance.addPostFrameCallback((_) {

    if (!mounted) return;
 
    _footwear = context.read<FootwearBleService>();

    _footwear!.onPower = _handlePowerDetected;
 
    _syncCamera();

  });

}
 
  @override

  void didUpdateWidget(covariant HomeScreen oldWidget) {

    super.didUpdateWidget(oldWidget);

    if (oldWidget.active != widget.active) {

      _syncCamera();

    }

  }
 
  @override

  void didChangeAppLifecycleState(AppLifecycleState state) {

    _appResumed = state == AppLifecycleState.resumed;

    _syncCamera();

  }
 
  Future<void> _syncCamera() async {

    // Serialize camera changes to avoid opening the camera

    // while a previous controller is still closing.

    if (_cameraOperation != null) {

      await _cameraOperation;

    }
 
    if (!mounted) return;
 
    final operation = _updateCamera();

    _cameraOperation = operation;
 
    try {

      await operation;

    } finally {

      if (identical(_cameraOperation, operation)) {

        _cameraOperation = null;

      }

    }

  }
 
  Future<void> _updateCamera() async {

    if (!_shouldUseCamera || await UnityHuntBridge.isGameRunning()) {

      await _releaseCamera();

      return;

    }
 
    if (_camera != null) return;
 
    CameraController? controller;
 
    try {

      final cameras = await availableCameras();
 
      if (cameras.isEmpty) {

        throw Exception('No camera available');

      }
 
      final rearCamera = cameras.firstWhere(

        (camera) => camera.lensDirection == CameraLensDirection.back,

        orElse: () => cameras.first,

      );
 
      controller = CameraController(

        rearCamera,

        ResolutionPreset.medium,

        enableAudio: false,

      );
 
      await controller.initialize();
 
      if (!_shouldUseCamera) {

        await controller.dispose();

        return;

      }
 
      setState(() {

        _camera = controller;

        _cameraMessage = null;

      });

    } catch (_) {

      if (controller != null) {

        await controller.dispose();

      }
 
      if (mounted) {

        setState(() {

          _cameraMessage = 'Camera unavailable. Check camera permission.';

        });

      }

    }

  }
 
  Future<void> _releaseCamera() async {

    final controller = _camera;

    _camera = null;
 
    if (mounted) setState(() {});
 
    await controller?.dispose();

  }
 
  Future<void> _playAR() async {

    if (_launching) return;
 
    setState(() => _launching = true);
 
    final footwear = context.read<FootwearBleService>();
    try {
      await footwear.disconnect();
      await _syncCamera();
      await UnityHuntBridge.launch();
    } catch (error) {
      await footwear.reconnectSavedShoe();

      if (mounted) {

        ScaffoldMessenger.of(context).showSnackBar(

          SnackBar(

            content: Text(error is PlatformException
                ? error.message ?? 'Unable to open the AR game.'
                : 'Unable to open the AR game.'),

          ),

        );

      }

    } finally {

      if (mounted) {

        setState(() => _launching = false);

        _syncCamera();

      }

    }

  }

void _handlePowerDetected() {

  if (!mounted) return;
 
  ScaffoldMessenger.of(context).showSnackBar(

    const SnackBar(

      content: Text('⚡ Power Detected!'),

      duration: Duration(seconds: 3),

      behavior: SnackBarBehavior.floating,

    ),

  );

}

  @override

  void dispose() {

  if (_footwear?.onPower == _handlePowerDetected) {

    _footwear?.onPower = null;

  }
 
  WidgetsBinding.instance.removeObserver(this);
 
  final controller = _camera;

  _camera = null;

  controller?.dispose();
 
  super.dispose();

}
 
  Widget _control({

    required IconData icon,

    required String label,

    required VoidCallback onTap,

  }) {

    return Material(

      color: background.withValues(alpha: 0.78),

      borderRadius: BorderRadius.circular(14),

      child: InkWell(

        onTap: onTap,

        borderRadius: BorderRadius.circular(14),

        child: Padding(

          padding: const EdgeInsets.symmetric(

            horizontal: 14,

            vertical: 12,

          ),

          child: Row(

            mainAxisSize: MainAxisSize.min,

            children: [

              Icon(icon, color: accent, size: 19),

              const SizedBox(width: 8),

              Text(

                label,

                style: const TextStyle(

                  color: Colors.white,

                  fontWeight: FontWeight.w600,

                  fontSize: 13,

                ),

              ),

            ],

          ),

        ),

      ),

    );

  }
 
  Widget _playButton({bool landscape = false}) {

    return Column(

      mainAxisSize: MainAxisSize.min,

      children: [

        Material(

          color: accent,

          shape: const CircleBorder(),

          elevation: 8,

          child: InkWell(

            customBorder: const CircleBorder(),

            onTap: _launching ? null : _playAR,

            child: SizedBox(

              width: landscape ? 92 : 110,

              height: landscape ? 92 : 110,

              child: const Icon(

                Icons.play_arrow_rounded,

                color: Colors.white,

                size: 54,

              ),

            ),

          ),

        ),

        const SizedBox(height: 12),

        Text(

          _launching ? 'OPENING...' : 'PLAY AR',

          style: const TextStyle(

            color: Colors.white,

            fontSize: 18,

            fontWeight: FontWeight.w800,

            letterSpacing: 2,

          ),

        ),

      ],

    );

  }
 
  Widget _stepsAndFootwear(

    int steps,

    FootwearBleService footwear,

  ) {

    return Container(

      padding: const EdgeInsets.symmetric(

        horizontal: 14,

        vertical: 10,

      ),

      decoration: BoxDecoration(

        color: const Color(0xFFFFFCF7).withValues(alpha: 0.96),

        borderRadius: BorderRadius.circular(14),

      ),

      child: Row(

        mainAxisSize: MainAxisSize.min,

        children: [

          const Icon(

            Icons.directions_walk_rounded,

            color: accent,

          ),

          const SizedBox(width: 8),

          Text(

            '$steps steps',

            style: const TextStyle(

              color: Color(0xFF722F42),

              fontWeight: FontWeight.w700,

            ),

          ),

          const SizedBox(width: 12),

          Container(

            width: 1,

            height: 20,

            color: Colors.white24,

          ),

          const SizedBox(width: 8),

          Flexible(

            child: Text(

              footwear.status,

              overflow: TextOverflow.ellipsis,

              style: const TextStyle(

                color: Colors.black,

                fontSize: 11,

              ),

            ),

          ),

          IconButton(

            tooltip: 'Connect footwear',

            visualDensity: VisualDensity.compact,

            onPressed: () => footwear.connect(),

            icon: const Icon(

              Icons.bluetooth_connected,

              color: accent,

              size: 20,

            ),

          ),

        ],

      ),

    );

  }
 
  Widget _portrait(

    int steps,

    FootwearBleService footwear,

  ) {

    return Padding(

      padding: const EdgeInsets.all(18),

      child: Column(

        children: [

          const Text(

            'MASAR+',

            style: TextStyle(

              color: Colors.white,

              fontSize: 25,

              fontWeight: FontWeight.w900,

              letterSpacing: 3,

            ),

          ),

          const SizedBox(height: 16),

          _stepsAndFootwear(steps, footwear),

          const Spacer(),

          _playButton(),

          const Spacer(),

          const SizedBox(height: 16),

        ],

      ),

    );

  }
 
  Widget _landscape(

    int steps,

    FootwearBleService footwear,

  ) {

    return Padding(

      padding: const EdgeInsets.symmetric(

        horizontal: 22,

        vertical: 10,

      ),

      child: Column(

        children: [

          Row(

            mainAxisAlignment: MainAxisAlignment.spaceBetween,

            children: [

              const Text(

                'MASAR+',

                style: TextStyle(

                  color: Colors.white,

                  fontSize: 22,

                  fontWeight: FontWeight.w900,

                  letterSpacing: 3,

                ),

              ),

              Flexible(

                child: _stepsAndFootwear(steps, footwear),

              ),

            ],

          ),

          Expanded(

            child: Center(

              child: _playButton(landscape: true),

            ),

          ),

        ],

      ),

    );

  }
 
  @override

  Widget build(BuildContext context) {

    final steps = context.watch<StepCounterService>().steps;

    final footwear = context.watch<FootwearBleService>();
 
    return Scaffold(

      backgroundColor: background,

      body: Stack(

        fit: StackFit.expand,

        children: [

          if (_camera?.value.isInitialized == true)

            CameraPreview(_camera!)

          else

            const ColoredBox(color: background),
 
          // Readability overlay.

          IgnorePointer(

            child: DecoratedBox(

              decoration: BoxDecoration(

                gradient: LinearGradient(

                  begin: Alignment.topCenter,

                  end: Alignment.bottomCenter,

                  colors: [
                      Colors.black.withValues(alpha: 0.38),
                      Colors.black.withValues(alpha: 0.08),
                      Colors.black.withValues(alpha: 0.35),
],

                ),

              ),

            ),

          ),
 
          SafeArea(

            child: LayoutBuilder(

              builder: (context, constraints) {

                final landscape = constraints.maxWidth > constraints.maxHeight;
 
                return landscape

                    ? _landscape(steps, footwear)

                    : _portrait(steps, footwear);

              },

            ),

          ),
 
          if (_cameraMessage != null)

            Positioned(

              left: 20,

              right: 20,

              bottom: 12,

              child: Text(

                _cameraMessage!,

                textAlign: TextAlign.center,

                style: const TextStyle(

                  color: Colors.white70,

                  fontSize: 12,

                ),

              ),

            ),

          if (_launching)
            const Positioned.fill(
              child: ColoredBox(
                color: Colors.black,
                child: Center(
                  child: Text('Loading…', style: TextStyle(color: Colors.white, fontSize: 22)),
                ),
              ),
            ),

        ],

      ),

    );

  }

}

 
