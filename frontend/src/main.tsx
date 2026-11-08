import React from "react"; // react
import ReactDOM from "react-dom/client"; // react dom
import App from "./App"; // main component
import "./styles.css"; // style

ReactDOM.createRoot(document.getElementById("root")!).render( // root-e render
  <React.StrictMode>
    <App />
  </React.StrictMode>
);