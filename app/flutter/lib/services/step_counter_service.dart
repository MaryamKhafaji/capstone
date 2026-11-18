import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

class StepCounterService extends ChangeNotifier {
  static const String _stepsKey = 'footwear_today_steps';
  static const String _dateKey = 'footwear_steps_date';

  int _steps = 0;

  int get steps => _steps;

  Future<void> initialize() async {
    final prefs = await SharedPreferences.getInstance();

    final today = _today();
    final savedDate = prefs.getString(_dateKey);

    if (savedDate == today) {
      _steps = prefs.getInt(_stepsKey) ?? 0;
    } else {
      _steps = 0;
      await prefs.setInt(_stepsKey, 0);
      await prefs.setString(_dateKey, today);
    }

    notifyListeners();
  }

  Future<void> incrementStep() async {
    await _checkForNewDay();

    _steps++;

    final prefs = await SharedPreferences.getInstance();
    await prefs.setInt(_stepsKey, _steps);

    notifyListeners();
  }

  Future<void> _checkForNewDay() async {
    final prefs = await SharedPreferences.getInstance();

    final today = _today();
    final savedDate = prefs.getString(_dateKey);

    if (savedDate != today) {
      _steps = 0;
      await prefs.setInt(_stepsKey, 0);
      await prefs.setString(_dateKey, today);
    }
  }

  String _today() {
    final now = DateTime.now();

    return '${now.year}-'
        '${now.month.toString().padLeft(2, '0')}-'
        '${now.day.toString().padLeft(2, '0')}';
  }
}