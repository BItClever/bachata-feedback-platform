import axios, { AxiosResponse } from 'axios';

const API_BASE_URL = 'http://localhost:5000/api';

// Создаем экземпляр axios
const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Интерцептор для добавления токена ко всем запросам
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
      console.log('Adding token to request:', config.url, 'Token exists:', !!token);
    } else {
      console.log('No token found for request:', config.url);
    }
    return config;
  },
  (error) => {
    console.error('Request interceptor error:', error);
    return Promise.reject(error);
  }
);

// Интерцептор для обработки ответов
api.interceptors.response.use(
  (response) => {
    console.log('Response received:', response.config.url, response.status);
    return response;
  },
  (error) => {
    console.error('Response error:', error.config?.url, error.response?.status, error.response?.data);
    
    if (error.response?.status === 401) {
      console.log('401 error - clearing tokens and redirecting to login');
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export interface User {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  nickname?: string;
  bio?: string;
  selfAssessedLevel?: string;
  startDancingDate?: string;
  danceStyles?: string;
  createdAt: string;
  updatedAt: string;
}

export interface Event {
  id: number; //
  name: string;
  description: string;
  date: string;
  location: string;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
  participantCount?: number;
  creatorName?: string;
  isUserParticipating?: boolean;
}

export interface Review {
  id: string;
  revieweeId: string;
  reviewerId: string;
  eventId: string;
  content: string;
  rating: number;
  isAnonymous: boolean;
  createdAt: string;
  updatedAt: string;
  reviewee: User;
  reviewer: User;
  event: Event;
}

// API endpoints
export const authAPI = {
  login: (credentials: { email: string; password: string }) =>
    api.post<{ token: string; user: User }>('/auth/login', credentials),
  register: (userData: {
    firstName: string;
    lastName: string;
    email: string;
    password: string;
    nickname?: string;
  }) => api.post<{ token: string; user: User }>('/auth/register', userData),
  getCurrentUser: () => api.get<User>('/auth/me'),
};

export const usersAPI = {
  getUsers: () => api.get<User[]>('/users'),
  getUser: (id: string) => api.get<User>(`/users/${id}`),
  getCurrentUser: () => api.get<User>('/users/me'),
  updateUser: (id: string, data: Partial<User>) => api.put<User>(`/users/${id}`, data),
};

export const reviewsAPI = {
  getReviews: () => api.get<Review[]>('/reviews'),
  getUserReviews: (userId: string) => api.get<Review[]>(`/reviews/user/${userId}`),
  createReview: (data: {
    revieweeId: string;
    eventId: string;
    content: string;
    rating: number;
    isAnonymous: boolean;
  }) => api.post<Review>('/reviews', data),
  updateReview: (id: string, data: Partial<Review>) => api.put<Review>(`/reviews/${id}`, data),
  deleteReview: (id: string) => api.delete(`/reviews/${id}`),
};

export const eventsAPI = {
  getEvents: () => api.get<Event[]>('/events'),
  getEvent: (id: number) => api.get<Event>(`/events/${id}`), 
  createEvent: (data: {
    name: string;
    description: string;
    date: string;
    location: string;
  }) => api.post<Event>('/events', data),
  updateEvent: (id: number, data: Partial<Event>) => api.put<Event>(`/events/${id}`, data),
  deleteEvent: (id: number) => api.delete(`/events/${id}`),
  joinEvent: (id: number) => api.post(`/events/${id}/join`),
  leaveEvent: (id: number) => api.post(`/events/${id}/leave`),
};

export default api;