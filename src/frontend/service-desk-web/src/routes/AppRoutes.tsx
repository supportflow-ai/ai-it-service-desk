import { BrowserRouter, Routes, Route } from 'react-router-dom';

function NotFound() {
  return (
    <div style={{ textAlign: 'center', padding: '4rem' }}>
      <h1>404</h1>
      <p>Page not found</p>
    </div>
  );
}

function Home() {
  return (
    <div style={{ textAlign: 'center', padding: '4rem' }}>
      <h1>AI IT Service Desk</h1>
      <p>Internal IT Service Request Management System</p>
      <p style={{ color: '#888', fontSize: '0.875rem' }}>
        Foundation scaffold — business features will be added in future sprints.
      </p>
    </div>
  );
}

export function AppRoutes() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="*" element={<NotFound />} />
      </Routes>
    </BrowserRouter>
  );
}
