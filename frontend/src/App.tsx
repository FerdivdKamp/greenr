// import { useState } from 'react'
// import reactLogo from './assets/react.svg'
// import viteLogo from '/vite.svg'
// import './App.css'

// function App() {
//   const [count, setCount] = useState(0)

//   return (
//     <>
//       <div>
//         <a href="https://vite.dev" target="_blank">
//           <img src={viteLogo} className="logo" alt="Vite logo" />
//         </a>
//         <a href="https://react.dev" target="_blank">
//           <img src={reactLogo} className="logo react" alt="React logo" />
//         </a>
//       </div>
//       <h1>Vite + React</h1>
//       <div className="card">
//         <button onClick={() => setCount((count) => count + 1)}>
//           count is {count}
//         </button>
//         <p>
//           Edit <code>src/App.tsx</code> and save to test HMR
//         </p>
//       </div>
//       <p className="read-the-docs">
//         Click on the Vite and React logos to learn more
//       </p>
//     </>
//   )
// }

// export default App


import { Link, Outlet } from "react-router-dom";

export default function App() {
  return (
    <div style={{ backgroundColor: "#939393ff", minHeight: "100vh" }}>
      <header
        style={{
          padding: "16px",
          borderBottom: "1px solid #a5a5a5ff",
          backgroundColor: "1px solid #b3b3b3ff",
          position: "sticky",
          // minHeight: "100vh",
          width: "90vw", // full window width
          top: 0,
        }}
      >
        <nav style={{ display: "flex", gap: "16px" }}>
          <Link to="/" style={{ color: "#22ea75ff", textDecoration: "none" }}>
            Home
          </Link>
          <Link
            to="/items"
            style={{ color: "#22ea75ff", textDecoration: "none" }}
          >
            Items
          </Link>
          <Link
            to="/questionnaires"
            style={{ color: "#22ea75ff", textDecoration: "none" }}
          >
            Questionnaires
          </Link>
        </nav>
      </header>
      <main style={{ padding: "16px" }}>
        <Outlet />
      </main>
    </div>
  );
}

