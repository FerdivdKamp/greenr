import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'items_page.dart';

const apiBaseUrl = String.fromEnvironment('API_BASE_URL', defaultValue: 'http://10.0.2.2:7285');

void main() => runApp(const CarbonTrackerApp());

class CarbonTrackerApp extends StatefulWidget {
  const CarbonTrackerApp({super.key, this.client});
  final http.Client? client;
  @override
  State<CarbonTrackerApp> createState() => _CarbonTrackerAppState();
}

class _CarbonTrackerAppState extends State<CarbonTrackerApp> {
  late final http.Client client = widget.client ?? http.Client();
  final identifier = TextEditingController();
  final password = TextEditingController();
  String? accessToken;
  String? refreshToken;
  String? error;
  bool busy = false;

  @override
  void dispose() {
    identifier.dispose();
    password.dispose();
    if (widget.client == null) client.close();
    super.dispose();
  }

  Future<void> login() async {
    setState(() { busy = true; error = null; });
    try {
      final response = await client.post(Uri.parse('$apiBaseUrl/api/auth/login'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'identifier': identifier.text.trim(), 'password': password.text}));
      if (!mounted) return;
      if (response.statusCode != 200) {
        setState(() => error = 'Sign-in failed. Check your credentials and try again.');
      } else {
        final tokens = jsonDecode(response.body) as Map<String, dynamic>;
        setState(() { accessToken = tokens['accessToken'] as String; refreshToken = tokens['refreshToken'] as String; password.clear(); });
      }
    } catch (_) {
      if (mounted) setState(() => error = 'Could not connect to the API.');
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> logout() async {
    final token = refreshToken;
    setState(() { accessToken = null; refreshToken = null; });
    if (token != null) {
      try {
        await client.post(Uri.parse('$apiBaseUrl/api/auth/logout'), headers: {'Content-Type': 'application/json'}, body: jsonEncode({'refreshToken': token}));
      } catch (_) { /* Local sign-out still succeeds if the server is unavailable. */ }
    }
  }

  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'Carbon Tracker', theme: ThemeData(primarySwatch: Colors.green),
    home: accessToken != null
      ? ItemsPage(client: client, accessToken: accessToken!, onSignOut: logout)
      : Scaffold(appBar: AppBar(title: const Text('Sign in')), body: Center(child: SingleChildScrollView(child: Padding(
          padding: const EdgeInsets.all(24), child: Column(mainAxisSize: MainAxisSize.min, children: [
            TextField(controller: identifier, decoration: const InputDecoration(labelText: 'Username or email')),
            TextField(controller: password, obscureText: true, decoration: const InputDecoration(labelText: 'Password')),
            if (error != null) Text(error!),
            ElevatedButton(onPressed: busy ? null : login, child: Text(busy ? 'Signing in...' : 'Sign in')),
          ]))))),
  );
}
