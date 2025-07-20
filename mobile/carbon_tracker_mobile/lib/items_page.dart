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
                price: item.price,
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

// Parameters
class _FootprintTrendChart extends StatelessWidget {
  final DateTime startDate;
  final double initialFootprint;
  final double price;

  const _FootprintTrendChart({
    required this.startDate,
    required this.initialFootprint,
    required this.price,
  });

  @override
  Widget build(BuildContext context) {
    final now = DateTime.now();
    final months = _generateMonthList(startDate, now);
    final totalMonths = max(1, months.length - 1);
    final useYears = months.length > 60;

    // Generate price and footprint per month
    final pricePerMonth = price / totalMonths;
    final footprintPerMonth = initialFootprint / totalMonths;


    final priceSpots = List.generate(
      months.length,
      (i) => FlSpot(i.toDouble(), (price - i * pricePerMonth).clamp(0, price)),
    );

    final footprintSpots = List.generate(
      months.length,
      (i) => FlSpot(i.toDouble(), (initialFootprint - i * footprintPerMonth).clamp(0, initialFootprint)),
    );

    return SizedBox(
      height: 200,
      child: LineChart(
  LineChartData(
    lineBarsData: [
      LineChartBarData(
        spots: priceSpots,
        isCurved: false,
        barWidth: 2,
        color: Colors.blue,
        dotData: const FlDotData(show: false),
      ),
      LineChartBarData(
        spots: footprintSpots,
        isCurved: false,
        barWidth: 2,
        color: Colors.green,
        dotData: const FlDotData(show: false),
      ),
    ],
    minX: 0,
    maxX: (months.length - 1).toDouble(),
    minY: 0,
    maxY: max(price, initialFootprint) * 1.2,
    gridData: const FlGridData(show: true),
    borderData: FlBorderData(show: true),
    titlesData: FlTitlesData(
      bottomTitles: AxisTitles(
        sideTitles: SideTitles(
          showTitles: true,
          interval: 1,
          getTitlesWidget: (value, meta) {
            final index = value.toInt();
            if (index < 0 || index >= months.length) return const SizedBox.shrink();
            final date = months[index];

            return Text(
              useYears ? '${date.year}' : '${date.month}/${date.year % 100}',
              style: const TextStyle(fontSize: 10),
            );
          },
        ),
      ),
      leftTitles: AxisTitles(
        sideTitles: SideTitles(
          showTitles: true,
          reservedSize: 40,
          getTitlesWidget: (value, meta) => Text('€${value.toStringAsFixed(0)}', style: const TextStyle(fontSize: 10)),
        ),
      ),
      rightTitles: AxisTitles(
        sideTitles: SideTitles(
          showTitles: true,
          reservedSize: 40,
          getTitlesWidget: (value, meta) => Text('${value.toStringAsFixed(1)}kg', style: const TextStyle(fontSize: 10)),
        ),
      ),
      topTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
    ),
    lineTouchData: LineTouchData(
      touchTooltipData: LineTouchTooltipData(
        fitInsideHorizontally : true,
        fitInsideVertically: true,
        getTooltipColor: (LineBarSpot spot) => const Color.fromARGB(40, 0, 0, 0),
        tooltipPadding: const EdgeInsets.all(8),
        getTooltipItems: (touchedSpots) {
          return touchedSpots.map((spot) {
            final index = spot.x.toInt();
            if (index < 0 || index >= months.length) return null;
            final date = months[index];
            final monthsSincePurchase = max(1, index);
            final pricePerMonth = price / monthsSincePurchase;
            final footprintPerMonth = initialFootprint / monthsSincePurchase;

            return LineTooltipItem(
              '${date.year}-${date.month.toString().padLeft(2, '0')}\n'
              '€${pricePerMonth.toStringAsFixed(2)} / mo\n'
              '${footprintPerMonth.toStringAsFixed(1)} kg CO₂ / mo',
              const TextStyle(color: Colors.black, fontSize: 12),
            );
          }).toList();
        },
      ),
    ),
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
