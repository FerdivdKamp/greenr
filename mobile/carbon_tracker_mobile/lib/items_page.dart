import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:url_launcher/url_launcher.dart';
import 'main.dart' show apiBaseUrl;

const websiteBaseUrl = String.fromEnvironment('WEBSITE_BASE_URL');

class Item {
  final String itemId, itemName, useCase;
  final double price, footprintKg;
  final DateTime? dateOfPurchase;
  Item(
      {required this.itemId,
      required this.itemName,
      required this.useCase,
      required this.price,
      required this.footprintKg,
      this.dateOfPurchase});
  factory Item.fromJson(Map<String, dynamic> json) => Item(
      itemId: json['itemId'] as String,
      itemName: json['itemName'] as String,
      useCase: json['useCase'] as String? ?? '',
      price: (json['price'] as num?)?.toDouble() ?? 0,
      footprintKg: (json['footprintKg'] as num?)?.toDouble() ?? 0,
      dateOfPurchase: json['dateOfPurchase'] == null
          ? null
          : DateTime.parse(json['dateOfPurchase'] as String));
}

class ItemsPage extends StatefulWidget {
  const ItemsPage({super.key, required this.client, required this.onSignOut});
  final http.Client client;
  final VoidCallback onSignOut;
  @override
  State<ItemsPage> createState() => _ItemsPageState();
}

class _ItemsPageState extends State<ItemsPage> {
  late Future<List<Item>> items;
  bool saving = false;
  Map<String, String> get headers => {'Content-Type': 'application/json'};
  Uri url(String path) => Uri.parse('$apiBaseUrl/api/items$path');
  @override
  void initState() {
    super.initState();
    items = fetchItems();
  }

  Future<List<Item>> fetchItems() async {
    late http.Response response;
    try {
      response = await widget.client.get(url(''), headers: headers);
    } catch (_) {
      throw Exception(
          'Could not connect to the API. Check the address and try again.');
    }
    if (response.statusCode != 200) {
      throw Exception('Could not load items. Please try again.');
    }
    return (jsonDecode(response.body) as List)
        .map((e) => Item.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  void reload() => setState(() {
        items = fetchItems();
      });
  void showError(String message) {
    if (mounted) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(message)));
    }
  }

  Future<void> openQuestionnaires() async {
    final base = Uri.tryParse(websiteBaseUrl);
    if (base == null ||
        !base.hasAuthority ||
        (base.scheme != 'https' && base.scheme != 'http') ||
        base.userInfo.isNotEmpty ||
        base.hasQuery ||
        base.hasFragment) {
      showError('The questionnaire website is not configured.');
      return;
    }
    final uri = base.replace(
        path: '${base.path.replaceAll(RegExp(r'/+$'), '')}/questionnaires');
    try {
      if (!await launchUrl(uri, mode: LaunchMode.externalApplication)) {
        showError('Could not open the questionnaire website.');
      }
    } catch (_) {
      showError('Could not open the questionnaire website.');
    }
  }

  void showQuestionnaires() {
    showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Questionnaires'),
        content: const Text(
            'Questionnaires open on the greenr website. Sign in there to view and complete them.'),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(dialogContext),
              child: const Text('Cancel')),
          TextButton(
              onPressed: () {
                Navigator.pop(dialogContext);
                openQuestionnaires();
              },
              child: const Text('Open website')),
        ],
      ),
    );
  }

  Future<void> edit([Item? item]) async {
    final result = await Navigator.of(context).push<Map<String, dynamic>>(
        MaterialPageRoute(builder: (_) => ItemForm(item: item)));
    if (result == null || !mounted) return;
    setState(() => saving = true);
    try {
      final response = item == null
          ? await widget.client
              .post(url(''), headers: headers, body: jsonEncode(result))
          : await widget.client.put(url('/${item.itemId}'),
              headers: headers, body: jsonEncode(result));
      if (response.statusCode < 200 || response.statusCode >= 300) {
        showError('Could not save item. Check the values and try again.');
      } else if (mounted) {
        reload();
      }
    } catch (_) {
      showError('Could not connect to the API. Please try again.');
    } finally {
      if (mounted) setState(() => saving = false);
    }
  }

  Future<void> remove(Item item) async {
    final confirmed = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
                title: const Text('Delete item?'),
                content: Text('Delete ${item.itemName}?'),
                actions: [
                  TextButton(
                      onPressed: () => Navigator.pop(context, false),
                      child: const Text('Cancel')),
                  TextButton(
                      onPressed: () => Navigator.pop(context, true),
                      child: const Text('Delete'))
                ]));
    if (confirmed != true || !mounted) return;
    setState(() => saving = true);
    try {
      final response =
          await widget.client.delete(url('/${item.itemId}'), headers: headers);
      if (response.statusCode != 204) {
        showError('Could not delete item. Please try again.');
      } else if (mounted) {
        reload();
      }
    } catch (_) {
      showError('Could not connect to the API. Please try again.');
    } finally {
      if (mounted) setState(() => saving = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
      appBar: AppBar(title: const Text('Items'), actions: [
        TextButton(onPressed: widget.onSignOut, child: const Text('Sign out'))
      ]),
      bottomNavigationBar: NavigationBar(
        selectedIndex: 0,
        onDestinationSelected: (index) {
          if (index == 1) showQuestionnaires();
        },
        destinations: const [
          NavigationDestination(icon: Icon(Icons.home_outlined), label: 'Home'),
          NavigationDestination(
              icon: Icon(Icons.assignment_outlined), label: 'Questionnaires'),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
          onPressed: saving ? null : () => edit(),
          label: const Text('Add item'),
          icon: const Icon(Icons.add)),
      body: FutureBuilder<List<Item>>(
          future: items,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return Center(
                  child: Column(mainAxisSize: MainAxisSize.min, children: [
                Padding(
                    padding: const EdgeInsets.all(16),
                    child: Text(
                        '${snapshot.error}'.replaceFirst('Exception: ', ''))),
                TextButton(onPressed: reload, child: const Text('Retry'))
              ]));
            }
            final list = snapshot.data!;
            final total =
                list.fold<double>(0, (sum, item) => sum + item.footprintKg);
            return ListView(children: [
              ListTile(
                  title: const Text('Total recorded footprint'),
                  subtitle: Text(
                      '${total.toStringAsFixed(3)} kg CO₂ · ${list.length} items')),
              if (list.isEmpty)
                const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('No items yet. Add your first item.')),
              ...list.map((item) => ListTile(
                  title: Text(item.itemName),
                  subtitle:
                      Text('${item.useCase} · ${item.footprintKg} kg CO₂'),
                  trailing: Wrap(children: [
                    IconButton(
                        tooltip: 'Edit ${item.itemName}',
                        onPressed: saving ? null : () => edit(item),
                        icon: const Icon(Icons.edit)),
                    IconButton(
                        tooltip: 'Delete ${item.itemName}',
                        onPressed: saving ? null : () => remove(item),
                        icon: const Icon(Icons.delete))
                  ]))),
              const SizedBox(height: 88),
            ]);
          }));
}

class ItemForm extends StatefulWidget {
  const ItemForm({super.key, this.item});
  final Item? item;
  @override
  State<ItemForm> createState() => _ItemFormState();
}

class _ItemFormState extends State<ItemForm> {
  final form = GlobalKey<FormState>();
  late final TextEditingController name, useCase, price, footprint, date;
  @override
  void initState() {
    super.initState();
    final i = widget.item;
    name = TextEditingController(text: i?.itemName);
    useCase = TextEditingController(text: i?.useCase);
    price = TextEditingController(text: i?.price.toString());
    footprint = TextEditingController(text: i?.footprintKg.toString());
    date = TextEditingController(
        text: i?.dateOfPurchase?.toIso8601String().substring(0, 10));
  }

  @override
  void dispose() {
    name.dispose();
    useCase.dispose();
    price.dispose();
    footprint.dispose();
    date.dispose();
    super.dispose();
  }

  String? number(String? value, int decimals, double max) {
    final text = value ?? '';
    final n = double.tryParse(text);
    if (!RegExp('^\\d+(\\.\\d{1,$decimals})?\$').hasMatch(text) ||
        n == null ||
        n > max) {
      return 'Enter a nonnegative number (up to $decimals decimal places).';
    }
    return null;
  }

  @override
  Widget build(BuildContext context) => Scaffold(
      appBar:
          AppBar(title: Text(widget.item == null ? 'Add item' : 'Edit item')),
      body: Form(
          key: form,
          child: ListView(padding: const EdgeInsets.all(16), children: [
            TextFormField(
                controller: name,
                maxLength: 50,
                decoration: const InputDecoration(labelText: 'Item name'),
                validator: (v) => (v == null || v.trim().isEmpty)
                    ? 'Enter an item name.'
                    : null),
            TextFormField(
                controller: useCase,
                maxLength: 20,
                decoration:
                    const InputDecoration(labelText: 'Use case (optional)')),
            TextFormField(
                controller: price,
                keyboardType:
                    const TextInputType.numberWithOptions(decimal: true),
                decoration: const InputDecoration(labelText: 'Price'),
                validator: (v) => number(v, 2, 99999999.99)),
            TextFormField(
                controller: footprint,
                keyboardType:
                    const TextInputType.numberWithOptions(decimal: true),
                decoration:
                    const InputDecoration(labelText: 'Footprint (kg CO₂)'),
                validator: (v) => number(v, 3, 9999999.999)),
            TextFormField(
                controller: date,
                decoration: const InputDecoration(
                    labelText: 'Purchase date (YYYY-MM-DD, optional)'),
                validator: (v) {
                  if (v == null || v.isEmpty) return null;
                  final parsed = DateTime.tryParse(v);
                  final today = DateTime.now().toUtc();
                  if (!RegExp(r'^\d{4}-\d{2}-\d{2}$').hasMatch(v) ||
                      parsed == null ||
                      parsed.toIso8601String().substring(0, 10) != v ||
                      parsed.year < 2 ||
                      parsed.isAfter(
                          DateTime.utc(today.year, today.month, today.day))) {
                    return 'Enter a valid date on or before today.';
                  }
                  return null;
                }),
            const SizedBox(height: 24),
            ElevatedButton(
                onPressed: () {
                  if (form.currentState!.validate()) {
                    Navigator.pop(context, {
                      'itemName': name.text.trim(),
                      'useCase': useCase.text.trim(),
                      'price': double.parse(price.text),
                      'footprintKg': double.parse(footprint.text),
                      'dateOfPurchase': date.text.isEmpty ? null : date.text
                    });
                  }
                },
                child: const Text('Save')),
          ])));
}
