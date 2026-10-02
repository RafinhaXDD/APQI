import { Route, Routes } from 'react-router'
import { Layout } from './components/Layout'
import {
  ConfirmEmailPage,
  ForgotPasswordPage,
  ResetPasswordPage,
} from './features/auth/EmailLinkPages'
import { LoginPage } from './features/auth/LoginPage'
import { RegisterPage } from './features/auth/RegisterPage'
import { RequireAuth } from './features/auth/RequireAuth'
import { HomePage, NotFoundPage } from './features/home/HomePage'
import { ProfilePage } from './features/profile/ProfilePage'

/** Routes only; providers live in main.tsx so tests can supply their own (e.g. MemoryRouter). */
export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<HomePage />} />
        <Route path="login" element={<LoginPage />} />
        <Route path="register" element={<RegisterPage />} />
        <Route path="confirm-email" element={<ConfirmEmailPage />} />
        <Route path="forgot-password" element={<ForgotPasswordPage />} />
        <Route path="reset-password" element={<ResetPasswordPage />} />
        <Route
          path="profile"
          element={
            <RequireAuth>
              <ProfilePage />
            </RequireAuth>
          }
        />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  )
}
