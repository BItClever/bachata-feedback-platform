import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { authAPI, User, usersAPI } from '../services/api';

interface RegisterData {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  nickname?: string;
}

interface AuthContextType {
  user: User | null;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (userData: RegisterData) => Promise<void>;
  logout: () => void;
  updateUserData: (userData: User) => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

interface AuthProviderProps {
  children: ReactNode;
}

export const AuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

useEffect(() => {
  const initializeAuth = async () => {
    const token = localStorage.getItem('token');
    const stored = localStorage.getItem('user');
    // Предзаполняем пользователя из localStorage, чтобы уменьшить «мерцание»
    if (token && stored) {
      try { setUser(JSON.parse(stored)); } catch {}
    }

    if (token) {
      try {
        const response = await authAPI.getCurrentUser()
        setUser(response.data);
        localStorage.setItem('user', JSON.stringify(response.data));
      } catch (error: any) {
        const status = error?.response?.status;
        if (status === 401) {
          localStorage.removeItem('token');
          localStorage.removeItem('user');
          setUser(null);
        } else {
          // не чистим токен на случайной ошибке сети
          console.log('Failed to load user from token (non-401):', error);
        }
      }
    }
    setIsLoading(false);
  };

  initializeAuth();
}, []);

  const login = async (email: string, password: string) => {
    const response = await authAPI.login({ email, password });
    const { token, user: userData } = response.data;
    
    console.log('Login successful, token:', token ? 'received' : 'not received');
    console.log('User data:', userData);
    
    localStorage.setItem('token', token);
    localStorage.setItem('user', JSON.stringify(userData));
    setUser(userData);
  };

  const register = async (userData: RegisterData) => {
    const response = await authAPI.register(userData);
    const { token, user: newUser } = response.data;
    
    console.log('Registration successful, token:', token ? 'received' : 'not received');
    
    localStorage.setItem('token', token);
    localStorage.setItem('user', JSON.stringify(newUser));
    setUser(newUser);
  };

  const logout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    setUser(null);
    console.log('User logged out');
  };

  const updateUserData = (userData: User) => {
    setUser(userData);
    localStorage.setItem('user', JSON.stringify(userData));
  };

  return (
    <AuthContext.Provider value={{ 
      user, 
      isLoading,
      login, 
      register, 
      logout, 
      updateUserData 
    }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};