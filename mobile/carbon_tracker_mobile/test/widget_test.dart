import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:carbon_tracker_mobile/main.dart';

void main() {
  testWidgets('sign in sends bearer token to items and signs out', (tester) async {
    final requests = <http.Request>[];
    final client = MockClient((request) async {
      requests.add(request);
      if (request.url.path == '/api/auth/login') {
        expect(jsonDecode(request.body)['identifier'], 'alice');
        return http.Response('{"accessToken":"access","refreshToken":"refresh"}', 200);
      }
      if (request.url.path == '/api/items') {
        expect(request.headers['Authorization'], 'Bearer access');
        return http.Response('[]', 200);
      }
      expect(request.url.path, '/api/auth/logout');
      expect(jsonDecode(request.body)['refreshToken'], 'refresh');
      return http.Response('', 204);
    });
    await tester.pumpWidget(CarbonTrackerApp(client: client));
    await tester.enterText(find.byType(TextField).first, 'alice');
    await tester.enterText(find.byType(TextField).last, 'password');
    await tester.tap(find.widgetWithText(ElevatedButton, 'Sign in'));
    await tester.pumpAndSettle();
    expect(find.text('Items'), findsOneWidget);
    await tester.tap(find.text('Sign out'));
    await tester.pumpAndSettle();
    expect(find.text('Username or email'), findsOneWidget);
    expect(requests.length, 3);
  });

  testWidgets('invalid credentials stay on sign in', (tester) async {
    final client = MockClient((_) async => http.Response('', 401));
    await tester.pumpWidget(CarbonTrackerApp(client: client));
    await tester.tap(find.widgetWithText(ElevatedButton, 'Sign in'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Sign-in failed'), findsOneWidget);
  });
}
