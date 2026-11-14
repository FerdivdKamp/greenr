# ADR 0001: Questionnaire access on mobile

Status: Accepted  
Date: 2026-11-14  
Related: [MO-04 / issue #29](https://github.com/FerdivdKamp/greenr/issues/29), [BE-05 / issue #19](https://github.com/FerdivdKamp/greenr/issues/19)

## Context

The React website already renders questionnaires using SurveyJS. The Flutter app currently focuses on signed-in item viewing. The backend restricts published definitions and response submission to authenticated users and supports a defined subset of SurveyJS questions. Mobile users may benefit from completing questionnaires on their phones, but an offline requirement has not been established.

## Decision

For the initial mobile release, questionnaires are available through the React website in the phone's browser; they are not part of the Flutter app. Users sign in to the website separately. This reuses the existing questionnaire implementation and avoids maintaining another renderer.

The next implementation to consider is embedding the website in the app using a web view, if an in-app experience becomes a priority. Before committing to it, validate authentication and session handling, navigation, secure deployment, and accessibility on supported devices. Do not assume the browser and web view share a session.

Defer a native Flutter questionnaire renderer until there is a demonstrated need that the web experience cannot meet, such as a validated offline requirement or a compelling native interaction. A native renderer would need to track the backend's supported question types, validation, and versioning behavior.

## Consequences and follow-up

- Users can complete questionnaires on a phone without installing or navigating through the app, but must switch to a browser and sign in there.
- Test the web flow on representative phones, including responsive layout, keyboard use, screen readers, validation, and submission errors. Accessibility is not established merely by using the web.
- Online access is assumed; offline completion and draft recovery are not promised. Confirm whether users need them before selecting a future implementation.
- The web view would reuse the web renderer but add platform integration and testing costs; native rendering would add a second client implementation and ongoing parity work.
