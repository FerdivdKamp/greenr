# Questionnaire-based footprint estimates and improvement plans

Status: Product design proposal  
Date: 2026-11-14

## Purpose

Help a person understand an estimate for a part of their footprint, choose a change that fits their life, and see how that change could affect the estimate over time. Each topic is optional: the result represents the activities covered by that topic, not the person's entire footprint. Small daily changes and larger, longer-term investments should both have a place.

This extends the [product vision](../backend/CarbonTracker.API/product-vision.md). Questionnaires initially run on the website, including on a phone; see [ADR 0001](adr/0001-mobile-questionnaires.md). The flow below describes proposed functionality, not functionality already implemented.

## User journey

1. **Choose a topic.** Explain what is included, the approximate time needed, and whether the topic estimates an individual or a household footprint.
2. **Describe the current situation.** Ask only for the inputs needed for an estimate, for example meals per week or commuting distance and modes. Offer an honest “I don't know” option and explain any default used. Save the answers and the period they represent.
3. **See a baseline.** Show the estimated kg CO2e for the stated scope and a comparable period, the main assumptions, and which inputs drive it. Let the user correct answers or stop here.
4. **Choose an improvement plan.** Offer a small number of achievable changes and a custom target where appropriate. Show the proposed activity and estimated footprint alongside the baseline, plus the estimated difference over the same period. Make the start date explicit. Choosing a plan is optional.
5. **Choose check-ins.** Offer weekly, monthly, or no check-ins, with no check-in as an equally valid choice. Explain that check-ins improve the estimate only for the periods they cover; they are not required to use the plan. Users can change the schedule or stop reminders at any time.
6. **Review progress.** Display the baseline, plan and period estimates, clearly distinguishing reported activity from assumptions. Let the user update, pause, replace, or end a plan and review its history without deleting earlier estimates.

## Candidate topics

These are ideas to prioritise and validate, not published questionnaires or claims about savings. Ask for quantities and alternatives rather than assuming that every substitution reduces emissions. Some changes depend on cost, health, housing, availability or accessibility; users decide what is realistic.

| Topic | Baseline questions | Example improvement plan | Possible check-in | Scale and considerations |
| --- | --- | --- | --- | --- |
| Meat and plant-based meals | Meals by type per week; meat type and approximate portions | Replace two meat meals a week with plant-based meals | How many meals of each type this period? | Small recurring choice; avoid dietary or moral judgments. |
| Dairy and alternatives | Milk, cheese and alternatives consumed per week | Change a chosen number of servings | Servings by type | Small recurring choice; different products and amounts matter. |
| Food waste | Food routinely thrown away and approximate quantities | Plan meals or use leftovers on chosen days | Amount or frequency discarded | Small recurring choice; rough quantities and disposal assumptions create uncertainty. |
| Commuting | Workdays, distance, mode, occupancy and remote days | Replace selected car commutes with cycling, public transport or working from home | Trips or distance by mode | Recurring; consider both directions and the energy effects of remote work. |
| Other local trips | Typical weekly trips, distances and modes | Walk, cycle or combine selected short trips | Trips or distance by mode | Small recurring choice; keep separate from commuting to avoid overlap. |
| Car use and vehicle choice | Distance, fuel or electricity, consumption and occupancy | Drive less, share journeys, or consider a different vehicle | Distance and fuel or electricity use | Can involve a large purchase; vehicle manufacture and charging must be scoped explicitly. |
| Home heating and energy use | Household energy bills or usage, heating fuel, floor area and household size where useful | Lower the thermostat when appropriate, adjust heating schedules, or improve insulation | Meter readings or energy use; optionally note weather or household changes | Daily habits through major investment; compare like seasons and avoid implying a health-compromising temperature. |
| Home energy label and renovation | Current label if known, dwelling and heating details, and actual usage where available | Explore insulation, glazing or a heating-system upgrade; record when installed | Milestones during planning; energy use after installation | Larger, often costly or landlord-dependent; a label alone cannot establish actual consumption or savings. |
| Electricity use and supply | Electricity usage and known supply details | Reduce a specific use or compare supply choices | Meter readings or usage | Avoid assuming a tariff has zero emissions; distinguish consumption from supply claims. |
| Flights and holidays | Trips, destinations or distances, transport and accommodation | Take fewer flights or choose an alternative trip | Record trips as they occur, or review yearly | Infrequent, potentially significant; an annual or event-based review may fit better than weekly reminders. |
| Purchases and electronics | Purchase frequency, type and expected replacement cycle | Keep a device longer, repair it, or buy refurbished | Purchases and repairs this period | Occasional decisions; coordinate with item tracking to avoid counting a purchase twice. |
| Clothing | Approximate purchases by type and replacement habits | Buy fewer new items or choose repair/second-hand | Purchases or repairs | Small, approachable actions; product-specific estimates may be uncertain. |
| Sports and hobbies | Travel to activities and significant equipment purchases | Change travel mode or extend equipment life | Trips and purchases | Can overlap with transport and item tracking; define boundaries first. |
| Hot water | Household hot-water use, shower frequency and duration, and water-heating fuel | Shorten selected showers or install an efficient showerhead | Typical shower duration or hot-water meter readings | Small recurring actions or a modest purchase; shared household use and water temperature affect the estimate. |
| Laundry and drying | Wash loads, usual temperature and drying method | Wash full loads at a suitable lower temperature or air-dry when practical | Loads washed and drying method | Small recurring choice; account for appliance energy and avoid unsafe hygiene assumptions. |
| Cooking | Typical use of an oven, hob or other appliances and their energy source | Batch-cook or choose an efficient appliance for suitable meals | Cooking sessions or estimated appliance use | Small recurring choice; benefits depend on appliance, portions and cooking time. |
| Deliveries and online shopping | Delivery frequency, approximate parcel sizes and return habits | Combine orders or choose fewer deliveries | Orders, deliveries and returns | Occasional or recurring; fulfilment and last-mile data may be unavailable, so estimates could be highly uncertain. |
| Furniture and home goods | Major purchases, expected lifetime and whether bought new or used | Repair, keep longer or choose second-hand for a planned purchase | Purchases, repairs or replacement decisions | Infrequent, sometimes costly decisions; separate embodied impacts from items already recorded elsewhere. |

Start with **one topic**, such as commuting or meals, chosen after validating user interest and finding suitable emission factors. Commuting is a useful candidate because a development example already exists; that example is not a complete calculation or tracking feature. A broad catalogue should not delay a reliable first journey.

## Estimates over time

For a given topic, define a baseline period, the activity quantities within its boundaries, and emission factors with compatible units. In the simplest case, estimated kg CO2e for a period is the sum of **activity quantity × applicable factor**. Convert quantities to the same period before comparing alternatives. Household results need a disclosed allocation rule before presenting a personal share.

Keep these concepts distinct:

- **Baseline estimate:** the user's reported or defaulted activity before the plan, normalised to a defined period.
- **Planned estimate:** the footprint if the chosen change is followed, using the same scope, period and calculation method as the baseline.
- **Period estimate:** with no check-in, project the plan from its start date and label it as an assumption, not observed performance. For a completed check-in, recalculate the period from the user's reported activity; mark any missing inputs that remain assumed.
- **Estimated reduction:** comparable baseline for the same duration minus the period estimate. A negative value is an estimated increase. Show the difference in kg CO2e and, where meaningful, a rounded percentage; do not claim it was measured or solely caused by the plan.

For illustration only, if an agreed baseline is 20 kg CO2e/week and a plan projects 15 kg/week, the projected reduction is 5 kg/week. If a check-in estimates 17 kg for a comparable week, that week's estimated reduction is 3 kg. These numbers illustrate the arithmetic, not real emission factors.

Check-ins should ask about **actual activity**, not merely whether a user succeeded. Specify the covered start and end dates, prevent overlapping check-ins from being counted twice, and allow corrections with a visible history. A missed check-in is not evidence of failure: retain the plan projection, mark the period as unconfirmed, and never silently treat it as reported activity. If a user changes or pauses a plan, apply the new assumption from its effective date and split affected periods where necessary. A later baseline update starts a new comparison period; do not silently rewrite past results.

Periodic totals should use covered days and comparable activity, accounting for seasonal variation, travel, household changes and other context where material. Avoid implying a full-year saving from a short or seasonal observation without making extrapolation explicit. If inputs are unavailable, show what is missing and either disclose a reasonable default or decline to calculate.

## Evidence, trust and scope

- Select and document appropriate, current emission-factor sources before implementation; record geography, year, units, boundaries and version. This design intentionally makes no numerical claims about actual savings.
- Store calculation inputs, factor and method versions, defaults, timestamps and the questionnaire definition/version used. Show users a plain-language explanation, uncertainty and limitations. Decide explicitly whether updated factors recalculate history or create a comparable new series.
- Separate operational energy use from embodied impacts such as renovation materials or vehicle manufacture; include these only with an explicit boundary and suitable data. Do not infer home savings from an energy label alone.
- Define mutually exclusive boundaries or an explicit deduplication rule before combining topic estimates with each other or with recorded items. Until then, show separate topic estimates and do not present their sum as a complete footprint.
- Keep responses, plans and check-ins private to the user. Support correction, export and deletion in line with the product's account-data capabilities; reminders require an explicit choice and an available delivery mechanism.
- Use supportive language. A plan is a personal choice, not a commitment enforced by the app; a higher estimate or missed check-in is useful information, not failure.

## Implementation implications and open decisions

The existing questionnaire backend stores published SurveyJS definitions and responses, but does not provide the calculation, plan, check-in, reminder or time-series model described here. It validates a [limited SurveyJS subset](../backend/CarbonTracker.API/QUESTIONNAIRES.md); conditional questions and expression-based calculations are not currently supported. Design the first questionnaire around that contract or separately scope any required backend changes.

Likely new domain concepts are a topic and its boundaries, baseline snapshot, versioned calculation/factors, plan with effective dates, check-in schedule and period response, and derived estimates with provenance. Responses and estimates must remain attributable to the signed-in owner. Delivery channels and permission to send reminders need separate design.

Before implementation, decide:

1. Which single topic and user problem to validate first, and what minimum inputs make its result useful?
2. Which factor sources, geography and allocation rules are credible and maintainable for that topic?
3. What is the baseline period, and how should seasonal or irregular activities be compared?
4. Which reminder channels are available, and how should missed, late or corrected check-ins behave in the interface?
5. How will overlapping topics and item purchases be kept separate or reconciled?
6. What level of explanation and uncertainty can users understand without making the journey burdensome?

Prototype the first flow on the website, test it with users and verify the calculation using worked examples before expanding to more topics or integrating aggregate totals.
