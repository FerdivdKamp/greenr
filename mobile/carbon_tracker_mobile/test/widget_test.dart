import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:carbon_tracker_mobile/main.dart';

void main() {
  testWidgets('sign in sends bearer token to items and signs out',
      (tester) async {
    final requests = <http.Request>[];
    final client = MockClient((request) async {
      requests.add(request);
      if (request.url.path == '/api/auth/login') {
        expect(jsonDecode(request.body)['identifier'], 'alice');
        return http.Response(
            '{"accessToken":"access","refreshToken":"refresh"}', 200);
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

  testWidgets('shows items, total and handles deletion', (tester) async {
    var exists = true;
    final client = MockClient((request) async {
      if (request.url.path == '/api/auth/login') {
        return http.Response('{"accessToken":"a","refreshToken":"r"}', 200);
      }
      if (request.method == 'DELETE') {
        exists = false;
        return http.Response('', 204);
      }
      return http.Response(
          exists
              ? '[{"itemId":"123","itemName":"Bike","useCase":"Travel","price":10,"footprintKg":2.5,"dateOfPurchase":null}]'
              : '[]',
          200);
    });
    await tester.pumpWidget(CarbonTrackerApp(client: client));
    await tester.tap(find.widgetWithText(ElevatedButton, 'Sign in'));
    await tester.pumpAndSettle();
    expect(find.text('Bike'), findsOneWidget);
    expect(find.textContaining('2.500 kg'), findsOneWidget);
    await tester.tap(find.byTooltip('Delete Bike'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(TextButton, 'Delete'));
    await tester.pumpAndSettle();
    expect(find.textContaining('No items yet'), findsOneWidget);
    expect(find.textContaining('0.000 kg'), findsOneWidget);
  });

  testWidgets('create and edit refresh the list and total', (tester) async {
    Map<String, dynamic>? item;
    final client = MockClient((request) async {
      if (request.url.path == '/api/auth/login') {
        return http.Response('{"accessToken":"a","refreshToken":"r"}', 200);
      }
      if (request.method == 'POST' || request.method == 'PUT') {
        expect(request.headers['Authorization'], 'Bearer a');
        item = {
          ...jsonDecode(request.body) as Map<String, dynamic>,
          'itemId': '123'
        };
        return http.Response('{}', request.method == 'POST' ? 201 : 200);
      }
      return http.Response(jsonEncode(item == null ? [] : [item]), 200);
    });
    await tester.pumpWidget(CarbonTrackerApp(client: client));
    await tester.tap(find.widgetWithText(ElevatedButton, 'Sign in'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Add item'));
    await tester.pumpAndSettle();
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Item name'), 'Bike');
    await tester.enterText(find.widgetWithText(TextFormField, 'Price'), '10');
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Footprint (kg CO₂)'), '2.5');
    await tester.ensureVisible(find.text('Save'));
    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();
    expect(find.textContaining('2.500 kg'), findsOneWidget);
    await tester.tap(find.byTooltip('Edit Bike'));
    await tester.pumpAndSettle();
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Footprint (kg CO₂)'), '4');
    await tester.ensureVisible(find.text('Save'));
    await tester.tap(find.text('Save'));
    await tester.pumpAndSettle();
    expect(find.textContaining('4.000 kg'), findsOneWidget);
  });

  testWidgets('item loading error offers retry and expired session guidance',
      (tester) async {
    final client = MockClient((request) async =>
        request.url.path == '/api/auth/login'
            ? http.Response('{"accessToken":"a","refreshToken":"r"}', 200)
            : http.Response('', 401));
    await tester.pumpWidget(CarbonTrackerApp(client: client));
    await tester.tap(find.widgetWithText(ElevatedButton, 'Sign in'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Session expired'), findsOneWidget);
    expect(find.text('Username or email'), findsOneWidget);
  });
}
