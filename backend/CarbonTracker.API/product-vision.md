# Greenr product vision

## Purpose

Greenr helps people understand and lower their personal carbon footprint. It should make individual choices visible, understandable, and motivating over time.

The product starts with the carbon footprint of **items** a person buys. It will later grow into a Strava-like personal-progress app: people can see their own trend, recognise lower-carbon choices, and work toward reducing their footprint without losing the context behind the numbers.

## The initial product promise

> Record the things you buy, understand their estimated carbon impact, and see how your choices improve over time.

The first useful experience is deliberately small:

1. A person creates an account and signs in.
2. They add an item, its purchase date, category/use, price, and estimated footprint in kg CO2e.
3. They can view and manage only their own items.
4. They see totals and a simple history/chart that explains their recorded footprint.

## Long-term direction: a Strava-like carbon journey

Greenr should reward *personal progress*, not just display a large total. A future dashboard can make reduction visible through:

- User-selected focus areas: a person chooses the parts of life they want to work on, such as commuting, home energy, eating, sports/hobbies, or vacations.
- A personal baseline: a clearly defined period such as a user's first three months of recorded activity.
- Progress versus self: footprint for the current month/quarter compared with the user's own baseline or equivalent prior period.
- Relative comparison: an opt-in comparison with a relevant aggregate, such as people in a similar country, household, or category of activity.
- Milestones and goals: for example, “10% lower item-related footprint than your baseline this quarter.”
- Trends and explanations: show which categories or entries changed the result, rather than presenting a score with no evidence.

Comparisons must always state their scope, data source, timeframe, and uncertainty. A footprint estimate is not a precise measurement; it should be labelled as kg CO2e and never be framed as a moral judgement or competition that punishes people for circumstances they cannot control.

### Reduction focus areas

Users should be able to select one or more focus areas and change them as their circumstances or interests change:

- **Commuting:** travel to work, transport mode, distance, and remote-work patterns.
- **Home:** household energy use, heating, insulation, and electricity choices.
- **Eating:** food choices and meal habits.
- **Sports and hobbies:** equipment, travel, and activity-specific purchases.
- **Vacations:** transport, accommodation, and trip choices.

The selected areas should personalise the dashboard, suggested questions, goals, and progress views. They should not be required for the item MVP, used to infer sensitive personal data, or treated as a complete measure of a person's footprint. A user can track only items at first, then opt into one focus area when it becomes useful.

## Product principles

- **Personal first:** private data belongs to the user; comparisons are optional and never expose individual data.
- **Useful before complete:** begin with a small, reliable item journey rather than attempting to model every source of emissions.
- **Explain the number:** show the category, estimate source/method, dates, and assumptions behind each calculation.
- **Progress over perfection:** celebrate sustained reduction against the user's own baseline, including honest gaps in data.
- **Trustworthy comparisons:** only compare like with like and make averages transparent; avoid false precision.
- **Accessible and calm:** clear language, readable charts, and no shame-based mechanics.

## Near-future TODOs

These are product-roadmap items to pick up after the foundation backlog in `issues.md`, especially DB-01 through BE-03 and one complete client journey.

- [ ] Define the item data model and validation rules: required fields, categories, units, dates, and whether price is optional.
- [ ] Decide how an item gets its carbon estimate for the MVP: user-entered value, a small curated catalogue, or both. Record its source and confidence level.
- [ ] Build the signed-in item CRUD journey in the chosen first client, including an empty state that teaches the first action.
- [ ] Add an API-backed personal summary: item count, total recorded kg CO2e, and category/date breakdown.
- [ ] Define one simple, honest chart: recorded footprint by month, with missing-data periods clearly shown.
- [ ] Write a short calculation glossary explaining kg CO2e, recorded versus estimated footprint, and why totals can change as estimates improve.
- [ ] Decide the initial comparison strategy before implementing it: self-only baseline first is recommended; add population averages only after a credible data source and matching criteria are available.
- [ ] Specify a baseline rule and goal rule, for example a rolling three-month baseline and a quarterly reduction goal, including how new users and sparse data are handled.
- [ ] Design a focus-area picker for commuting, home, eating, sports/hobbies, and vacations; allow multiple selections, changes over time, and a “not sure yet” path.
- [ ] Define the smallest useful questionnaire/data set for each focus area, beginning with one area rather than collecting every lifestyle detail at onboarding.
- [ ] Ensure goals, recommendations, and comparison views respect the user's selected focus areas and make any assumptions visible.
- [ ] Add user controls for data export, account deletion, and opting in/out of aggregate comparison before any social or ranking feature.
- [ ] Run a small usability test with a few people: can they add an item, interpret the chart, and explain what change they could make next?

## Future feature candidates

After the item experience is reliable, candidate expansions include commute/home/lifestyle questionnaires, a footprint-estimate catalogue, personal goals, opt-in aggregate benchmarks, and shareable progress summaries. Each should earn its place by making a user more able to understand or reduce their own footprint.
