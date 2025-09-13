# React + TypeScript + Vite


To start locally
```
npm run dev
```


### Folder structure  
src/features/ → group related components + API calls

src/components/ → for shared UI bits

src/lib/ → for utilities (like API client, env config)



```
src/
  app/
    router.tsx            # routes definition
    queryClient.ts        # React Query client
    providers.tsx         # (optional) global contexts
  pages/
    HomePage.tsx          # route-level component (URL = "/")
    ItemsPage.tsx         # route-level component (URL = "/items")
  features/
    items/
      api.ts              # calls backend (/Items, mutations, etc.)
      types.ts            # Item type(s)
      hooks.ts            # useItems(), useCreateItem() etc.
      components/
        ItemsTable.tsx    # UI specific to "items" feature (used by ItemsPage)
      __tests__/...       # (optional)
  components/
    Button.tsx            # shared UI primitives (used anywhere)
    Modal.tsx
    DataTable.tsx         # generic table you can reuse across features
  lib/
    http.ts               # fetch wrapper
    env.ts                # runtime config
    format.ts             # e.g., money/date formatters
  styles/
    index.css
  App.tsx                 # app shell/layout (header/nav + <Outlet/>)
  main.tsx                # ReactDOM + providers

```


```
frontend/
├── public/                     # Static assets (copied as-is to build)
│   └── vite.svg
├── src/                        # Application source code
│   ├── app/                    # Global app setup
│   │   ├── queryClient.ts      # React Query client (API cache manager)
│   │   └── router.tsx          # React Router configuration (pages, routes)
│   │
│   ├── features/               # Feature-based modules
│   │   └── items/              # Example feature: Items
│   │       ├── ItemsPage.tsx   # React component (UI with table + button)
│   │       ├── api.ts          # API calls for Items (http.get("/Items"))
│   │       └── types.ts        # TypeScript interfaces for Item
│   │
│   ├── home/                   # Example "Home" page
│   │   └── Home.tsx
│   │
│   ├── lib/                    # Reusable utilities
│   │   ├── env.ts              # Centralized environment variables
│   │   └── http.ts             # HTTP helper (fetch wrapper)
│   │
│   ├── App.tsx                 # App layout (header + navigation)
│   ├── main.tsx                # Application entry (ReactDOM + providers)
│   ├── index.css               # Global styles
│   └── vite-env.d.ts           # Vite TypeScript types
│
├── .eslintrc.js / eslint.config.js # ESLint config (linting rules)
├── package.json                # Project dependencies + scripts
├── tsconfig.json               # TypeScript compiler config
├── vite.config.ts              # Vite build/dev config (proxy, plugins)
└── typedoc.json                # TypeDoc config for API documentation

```


## TypeDoc
Technical documentation 

`npm run docs`
Stores the html documation in subfolder typedoc, you can just open index.html

`npm run docs:open`
Runs the documentation in a local server



## StoryBooks
TO BE ADDED


# Template stuff
This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Babel](https://babeljs.io/) for Fast Refresh
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/) for Fast Refresh

## Expanding the ESLint configuration

If you are developing a production application, we recommend updating the configuration to enable type-aware lint rules:

```js
export default tseslint.config([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      // Other configs...

      // Remove tseslint.configs.recommended and replace with this
      ...tseslint.configs.recommendedTypeChecked,
      // Alternatively, use this for stricter rules
      ...tseslint.configs.strictTypeChecked,
      // Optionally, add this for stylistic rules
      ...tseslint.configs.stylisticTypeChecked,

      // Other configs...
    ],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.node.json', './tsconfig.app.json'],
        tsconfigRootDir: import.meta.dirname,
      },
      // other options...
    },
  },
])
```

You can also install [eslint-plugin-react-x](https://github.com/Rel1cx/eslint-react/tree/main/packages/plugins/eslint-plugin-react-x) and [eslint-plugin-react-dom](https://github.com/Rel1cx/eslint-react/tree/main/packages/plugins/eslint-plugin-react-dom) for React-specific lint rules:

```js
// eslint.config.js
import reactX from 'eslint-plugin-react-x'
import reactDom from 'eslint-plugin-react-dom'

export default tseslint.config([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      // Other configs...
      // Enable lint rules for React
      reactX.configs['recommended-typescript'],
      // Enable lint rules for React DOM
      reactDom.configs.recommended,
    ],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.node.json', './tsconfig.app.json'],
        tsconfigRootDir: import.meta.dirname,
      },
      // other options...
    },
  },
])
```
