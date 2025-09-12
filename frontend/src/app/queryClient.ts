// This file sets up a "QueryClient" for React Query (aka TanStack Query).
// The QueryClient is like a central manager that keeps track of all API requests,
// their cached results, and rules for when to refetch data.

// Import the QueryClient class from React Query
import { QueryClient } from "@tanstack/react-query";

// Create a single instance of QueryClient for the whole app.
// Think of this as the "store" that holds cached API responses.
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // By default, React Query refetches data when you focus the browser window.
      // We disable that here to avoid surprises (e.g., leaving and coming back triggers fetch).
      refetchOnWindowFocus: false,
    },
  },
});

// Later, in src/main.tsx, we'll wrap our app in <QueryClientProvider client={queryClient}>
// so every component can use hooks like useQuery() or useMutation() to talk to the backend.
