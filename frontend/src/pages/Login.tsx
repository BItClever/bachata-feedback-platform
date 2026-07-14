import React, { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth, TelegramAuthData } from '../contexts/AuthContext';
import { useTranslation } from 'react-i18next';
import { telegramAuthAPI } from '../services/api';

// Расширяем window для Telegram Widget callback
declare global {
  interface Window {
    onTelegramAuth?: (user: TelegramAuthData) => void;
  }
}

const Login: React.FC = () => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [botName, setBotName] = useState<string | null>(null);
  const [showEmailForm, setShowEmailForm] = useState(false);
  const telegramWidgetRef = useRef<HTMLDivElement>(null);

  const { login, loginWithTelegram } = useAuth();
  const navigate = useNavigate();
  const { t } = useTranslation();

  // Загружаем имя бота с бэкенда
  useEffect(() => {
    telegramAuthAPI.getBotName()
      .then(res => setBotName(res.data.botName))
      .catch(() => {
        // Если Telegram не настроен — показываем только email-форму
        setShowEmailForm(true);
      });
  }, []);

  // Инициализируем Telegram Login Widget после получения botName
  const handleTelegramAuth = useCallback(async (data: TelegramAuthData) => {
    setError('');
    setIsLoading(true);
    console.log('[TelegramAuth] callback received, id=', data.id, 'auth_date=', data.auth_date);
    try {
      const { isNewUser } = await loginWithTelegram(data);
      console.log('[TelegramAuth] success, isNewUser=', isNewUser);
      navigate(isNewUser ? '/onboarding' : '/dashboard');
    } catch (err: any) {
      const msg = err?.response?.data?.message || err?.message || t('errors.loginFailed');
      console.error('[TelegramAuth] failed:', err?.response?.status, msg, err);
      setError(msg);
    } finally {
      setIsLoading(false);
    }
  }, [loginWithTelegram, navigate, t]);

  useEffect(() => {
    if (!botName || !telegramWidgetRef.current) return;

    // Регистрируем глобальный callback для виджета
    window.onTelegramAuth = handleTelegramAuth;

    // Создаём script-элемент виджета
    const script = document.createElement('script');
    script.src = 'https://telegram.org/js/telegram-widget.js?22';
    script.setAttribute('data-telegram-login', botName);
    script.setAttribute('data-size', 'large');
    script.setAttribute('data-onauth', 'onTelegramAuth(user)');
    script.setAttribute('data-request-access', 'write');
    script.async = true;

    // Очищаем контейнер перед добавлением (React StrictMode вызывает эффект дважды)
    telegramWidgetRef.current.innerHTML = '';
    telegramWidgetRef.current.appendChild(script);

    return () => {
      delete window.onTelegramAuth;
    };
  }, [botName, handleTelegramAuth]);

  const handleEmailSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      await login(email, password);
      navigate('/dashboard');
    } catch (err: any) {
      setError(err.response?.data?.message || t('errors.loginFailed'));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-md w-full space-y-8">
        <div>
          <h2 className="mt-6 text-center text-3xl font-extrabold text-gray-900">
            {t('auth.login.title')}
          </h2>
          <p className="mt-2 text-center text-sm text-gray-600">
            {t('auth.login.subtitle', 'Войдите, чтобы продолжить')}
          </p>
        </div>

        {/* Telegram Widget — основной способ входа */}
        {botName && (
          <div className="flex flex-col items-center space-y-4">
            <div ref={telegramWidgetRef} className="flex justify-center" />
            {isLoading && (
              <p className="text-sm text-gray-500">{t('auth.login.signingIn')}</p>
            )}
          </div>
        )}

        {error && (
          <div className="bg-red-50 border border-red-400 text-red-700 px-4 py-3 rounded">
            {error}
          </div>
        )}

        {/* Разделитель */}
        {botName && (
          <div className="relative">
            <div className="absolute inset-0 flex items-center">
              <div className="w-full border-t border-gray-300" />
            </div>
            <div className="relative flex justify-center text-sm">
              <button
                type="button"
                onClick={() => setShowEmailForm(v => !v)}
                className="px-2 bg-gray-50 text-gray-500 hover:text-gray-700 underline"
              >
                {showEmailForm
                  ? t('auth.login.hideEmailForm', 'Скрыть форму входа')
                  : t('auth.login.adminLogin', 'Войти по email (только для администраторов)')}
              </button>
            </div>
          </div>
        )}

        {/* Email/Password — fallback для администраторов */}
        {showEmailForm && (
          <form className="mt-4 space-y-4" onSubmit={handleEmailSubmit}>
            <div>
              <label htmlFor="email" className="block text-sm font-medium text-gray-700">
                {t('auth.login.email')}
              </label>
              <input
                id="email"
                name="email"
                type="email"
                required
                autoComplete="email"
                className="input-field"
                placeholder={t('auth.login.email') || 'Email'}
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>
            <div>
              <label htmlFor="password" className="block text-sm font-medium text-gray-700">
                {t('auth.login.password')}
              </label>
              <div className="relative">
                <input
                  id="password"
                  name="password"
                  type={showPassword ? 'text' : 'password'}
                  required
                  autoComplete="current-password"
                  className="input-field pr-10"
                  placeholder={t('auth.login.password') || 'Password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(v => !v)}
                  className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-500"
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                >
                  {showPassword ? '🙈' : '👁️'}
                </button>
              </div>
            </div>

            <button
              type="submit"
              disabled={isLoading}
              className="btn-primary w-full"
            >
              {isLoading ? t('auth.login.signingIn') : t('auth.login.signIn')}
            </button>
          </form>
        )}
      </div>
    </div>
  );
};

export default Login;
