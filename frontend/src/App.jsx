import React from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { FilterProvider } from './context/FilterContext';
import { Navbar } from './components/Navbar';
import { BrowsePage } from './pages/BrowsePage';
import { MyListingsPage } from './pages/MyListingsPage';
import { SettingsPage } from './pages/SettingsPage';
import { ReportPage } from './pages/ReportPage';

function App() {
  return (
    <BrowserRouter>
      <FilterProvider>
        <div className="min-h-screen bg-gray-50">
          <Navbar />
          <Routes>
            <Route path="/" element={<BrowsePage />} />
            <Route path="/my-listings" element={<MyListingsPage />} />
            <Route path="/settings" element={<SettingsPage />} />
            <Route path="/reports" element={<ReportPage />} />
          </Routes>
        </div>
      </FilterProvider>
    </BrowserRouter>
  );
}

export default App;
