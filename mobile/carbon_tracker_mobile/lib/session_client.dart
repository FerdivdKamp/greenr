import 'dart:convert';
import 'package:http/http.dart' as http;

/// Holds one in-memory session. All authenticated requests share refresh rotation.
class SessionClient extends http.BaseClient {
  SessionClient(this.inner, this.baseUrl, this.accessToken, this.refreshToken,
      this.onExpired);
  final http.Client inner;
  final String baseUrl;
  final void Function() onExpired;
  String accessToken;
  String refreshToken;
  Future<bool>? _refreshing;
  bool _closed = false;

  Future<bool> _refresh() async {
    final response = await inner.post(Uri.parse('$baseUrl/api/auth/refresh'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'refreshToken': refreshToken}));
    if (response.statusCode == 401) {
      if (!_closed) {
        _closed = true;
        onExpired();
      }
      return false;
    }
    if (response.statusCode != 200) {
      throw http.ClientException(
          'Could not refresh the session. Please try again.');
    }
    final tokens = jsonDecode(response.body) as Map<String, dynamic>;
    accessToken = tokens['accessToken'] as String;
    refreshToken = tokens['refreshToken'] as String;
    return true;
  }

  Future<bool> _refreshOnce() async {
    final existing = _refreshing;
    if (existing != null) return existing;
    final future = _refresh();
    _refreshing = future;
    try {
      return await future;
    } finally {
      _refreshing = null;
    }
  }

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    if (_closed) throw http.ClientException('Session ended.');
    final body = await request.finalize().toBytes();
    final usedToken = accessToken;
    Future<http.StreamedResponse> sendWith(String token) {
      final copy = http.Request(request.method, request.url)
        ..headers.addAll(request.headers)
        ..headers['Authorization'] = 'Bearer $token'
        ..bodyBytes = body
        ..followRedirects = request.followRedirects
        ..maxRedirects = request.maxRedirects
        ..persistentConnection = request.persistentConnection;
      return inner.send(copy);
    }

    var response = await sendWith(usedToken);
    if (response.statusCode != 401 || _closed) return response;
    await response.stream.drain();
    if (usedToken == accessToken && !await _refreshOnce()) {
      return http.StreamedResponse(const Stream.empty(), 401);
    }
    if (_closed) throw http.ClientException('Session ended.');
    response = await sendWith(accessToken);
    if (response.statusCode == 401 && !_closed) {
      _closed = true;
      onExpired();
    }
    return response;
  }

  Future<void> signOut() async {
    _closed = true;
    try {
      await _refreshing;
    } catch (_) {
      // Still attempt to revoke the last known token.
    }
    try {
      await inner.post(Uri.parse('$baseUrl/api/auth/logout'),
          headers: {'Content-Type': 'application/json'},
          body: jsonEncode({'refreshToken': refreshToken}));
    } catch (_) {
      // Local sign-out succeeds even if the server cannot be reached.
    }
  }
}
