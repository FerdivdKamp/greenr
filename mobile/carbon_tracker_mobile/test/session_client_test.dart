import 'dart:async';
import 'dart:convert';
import 'package:carbon_tracker_mobile/session_client.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test(
      'simultaneous expired requests share rotation and logout revokes new token',
      () async {
    final gate = Completer<void>();
    var refreshes = 0;
    final inner = MockClient((request) async {
      if (request.url.path == '/api/auth/refresh') {
        refreshes++;
        expect(jsonDecode(request.body)['refreshToken'], 'old-refresh');
        await gate.future;
        return http.Response(
            '{"accessToken":"new","refreshToken":"new-refresh"}', 200);
      }
      if (request.url.path == '/api/auth/logout') {
        expect(jsonDecode(request.body)['refreshToken'], 'new-refresh');
        return http.Response('', 204);
      }
      return http.Response(
          '', request.headers['Authorization'] == 'Bearer new' ? 200 : 401);
    });
    var expired = false;
    final session = SessionClient(
        inner, 'http://localhost', 'old', 'old-refresh', () => expired = true);
    final first = session.get(Uri.parse('http://localhost/api/items'));
    final second = session.get(Uri.parse('http://localhost/api/items'));
    await Future<void>.delayed(Duration.zero);
    gate.complete();
    expect((await first).statusCode, 200);
    expect((await second).statusCode, 200);
    expect(refreshes, 1);
    expect(expired, false);
    await session.signOut();
  });

  test('failed refresh expires the session', () async {
    final inner = MockClient((_) async => http.Response('', 401));
    var expired = 0;
    final session = SessionClient(
        inner, 'http://localhost', 'old', 'refresh', () => expired++);
    final result = await session.get(Uri.parse('http://localhost/api/items'));
    expect(result.statusCode, 401);
    expect(expired, 1);
  });

  test('sign out while refresh is pending revokes rotated token', () async {
    final started = Completer<void>();
    final gate = Completer<void>();
    String? revoked;
    final inner = MockClient((request) async {
      if (request.url.path == '/api/auth/refresh') {
        started.complete();
        await gate.future;
        return http.Response(
            '{"accessToken":"new","refreshToken":"rotated"}', 200);
      }
      if (request.url.path == '/api/auth/logout') {
        revoked = jsonDecode(request.body)['refreshToken'] as String;
        return http.Response('', 204);
      }
      return http.Response('', 401);
    });
    final session =
        SessionClient(inner, 'http://localhost', 'old', 'refresh', () {});
    final request = session.get(Uri.parse('http://localhost/api/items'));
    // Attach the error handler before closing the session.
    final outcome =
        request.then<Object>((response) => response, onError: (Object e) => e);
    await started.future;
    final signOut = session.signOut();
    gate.complete();
    await signOut;
    expect(await outcome, isA<http.ClientException>());
    expect(revoked, 'rotated');
  });
}
