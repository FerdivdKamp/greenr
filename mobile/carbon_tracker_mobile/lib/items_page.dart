import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:fl_chart/fl_chart.dart';
import 'dart:convert';
import 'dart:math';

class Item {
  final String itemId;
  final String itemName;
  final String useCase;
  final double price;
  final double footprintKg;
  final DateTime? dateOfPurchase;

  Item({
    required this.itemId,
    required this.itemName,
    required this.useCase,
    required this.price,
    required this.footprintKg,
    this.dateOfPurchase,
  });

  factory Item.fromJson(Map<String, dynamic> json) {
    return Item(
      itemId: json['itemId'],
      itemName: json['itemName'],
      useCase: json['useCase'],
      price: (json['price'] as num).toDouble(),
      footprintKg: (json['footprintKg'] as num).toDouble(),
      dateOfPurchase: json['dateOfPurchase'] != null
          ? DateTime.tryParse(json['dateOfPurchase'])
          : null,
    );
  }
}

class ItemsPage extends StatefulWidget {
  const ItemsPage({super.key});

  @override
  _ItemsPageState createState() => _ItemsPageState();
}

class _ItemsPageState extends State<ItemsPage> {
  late Future<List<Item>> items;

  @override
  void initState() {
    super.initState();
    items = fetchItems();
  }

  Future<List<Item>> fetchItems() async {
    final response = await http.get(Uri.parse('http://10.0.2.2:7285/items'));

    if (response.statusCode == 200) {
      final List<dynamic> body = json.decode(response.body);
      return body.map((json) => Item.fromJson(json)).toList();
    } else {
      throw Exception('Failed to load items');
    }
  }

  Widget _buildItemList(List<Item> items) {
    return ListView.builder(
      itemCount: items.length,
      itemBuilder: (context, index) {
        final item = items[index];
        return ExpansionTile(
          title: Text(item.itemName),
          subtitle: Text('${item.useCase} — €${item.price.toStringAsFixed(0)}'),
          trailing: Text('${item.footprintKg} kg CO₂'),
          children: [
            Padding(
              padding: const EdgeInsets.all(16.0),
              child: _FootprintTrendChart(
                startDate: item.dateOfPurchase ?? DateTime.now().subtract(const Duration(days: 365)),
                initialFootprint: item.footprintKg,
              ),
            ),
          ],
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Items')),
      body: FutureBuilder<List<Item>>(
        future: items,
        builder: (context, snapshot) {
          if (snapshot.hasData) {
            return _buildItemList(snapshot.data!);
          } else if (snapshot.hasError) {
            return Center(child: Text('Error: ${snapshot.error}'));
          }
          return const Center(child: CircularProgressIndicator());
        },
      ),
    );
  }
}

// ✅ Move this outside the State class
class _FootprintTrendChart extends StatelessWidget {
  final DateTime startDate;
  final double initialFootprint;

  const _FootprintTrendChart({
    required this.startDate,
    required this.initialFootprint,
  });

  @override
  Widget build(BuildContext context) {
    final now = DateTime.now();
    final months = _generateMonthList(startDate, now);
    final step = initialFootprint / max(1, (months.length - 1));

    return SizedBox(
      height: 200,
      child: LineChart(
        LineChartData(
          lineBarsData: [
            LineChartBarData(
              spots: List.generate(
                months.length,
                (i) => FlSpot(
                  i.toDouble(),
                  (initialFootprint - i * step).clamp(0, initialFootprint),
                ),
              ),
              isCurved: true,
              barWidth: 2,
              color: Colors.green,
              dotData: const FlDotData(show: false),
            ),
          ],
          gridData: const FlGridData(show: true),
          titlesData: FlTitlesData(
            leftTitles: const AxisTitles(
              sideTitles: SideTitles(showTitles: true),
            ),
            bottomTitles: AxisTitles(
              sideTitles: SideTitles(
                showTitles: true,
                interval: 1,
                getTitlesWidget: (value, meta) {
                  final index = value.toInt();
                  if (index < 0 || index >= months.length) return const SizedBox.shrink();
                  final month = months[index];
                  return Text('${month.month}/${month.year % 100}', style: const TextStyle(fontSize: 10));
                },
              ),
            ),
            topTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
            rightTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
          ),
          borderData: FlBorderData(show: true),
          minX: 0,
          maxX: (months.length - 1).toDouble(),
          minY: 0,
          maxY: initialFootprint,
        ),
      ),
    );
  }

  List<DateTime> _generateMonthList(DateTime from, DateTime to) {
    final months = <DateTime>[];
    DateTime current = DateTime(from.year, from.month);
    while (current.isBefore(to)) {
      months.add(current);
      current = DateTime(current.year, current.month + 1);
    }
    months.add(current); // include the end month
    return months;
  }
}
