import { BrowserRouter, Routes, Route } from "react-router-dom";
import Layout from "./components/layouts/layout";
import DashboardPage from "./pages/DashboardPage";
import AnalyzePage from "./pages/AnalyzePage";
import RepositoryListPage from "./pages/RepositoryListPage";
import RepositoryDetailPage from "./pages/RepositoryDetailPage";
import { AppProvider } from "./context/AppContext";

const App = () => {
  return (
    <AppProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<Layout />} >
            <Route path="/" element={<DashboardPage />} />
            <Route path="/analyze" element={<AnalyzePage />} />
            <Route path="/repositories" element={<RepositoryListPage />} />
            <Route path="/repository/:id" element={<RepositoryDetailPage />} />
            <Route path="/analyze/:id" element={<RepositoryDetailPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </AppProvider>
  );
}

export default App;