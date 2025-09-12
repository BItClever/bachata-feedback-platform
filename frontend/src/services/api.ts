import axios from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000/api';

// Создаем экземпляр axios
const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Интерцептор: добавляем токен
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    if (token) {
      (config.headers as any).Authorization = 'Bearer ${ token }';
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Интерцептор: обработка ответов/ошибок
api.interceptors.response.use(
  (response) => response,
  (error) => {
    // 401 — выходим и на /login
    if (error.response?.status === 401) {
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      window.location.href = '/login';
      return;
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
  dancerRole?: string; // "Lead" | "Follow" | "Both"
  roles?: string[];
  permissions?: string[];
}

export interface Event {
  id: number;
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
  id: number;
  reviewerId: string;
  revieweeId: string;
  reviewerName?: string;
  revieweeName?: string;
  eventId?: number | null;
  eventName?: string | null;
  leadRatings?: { [key: string]: number };
  followRatings?: { [key: string]: number };
  textReview?: string;
  tags?: string[];
  isAnonymous: boolean;
  createdAt: string;
}

export interface CreateUserReviewPayload {
  revieweeId: string;
  eventId?: number | null;
  leadRatings?: { [key: string]: number };
  followRatings?: { [key: string]: number };
  textReview?: string;
  tags?: string[];
  isAnonymous: boolean;
}

export interface EventReview {
  id: number;
  eventId: number;
  eventName: string;
  reviewerId: string;
  reviewerName: string;
  ratings?: { [key: string]: number };
  textReview?: string;
  tags?: string[];
  isAnonymous: boolean;
  createdAt: string;
}

export interface CreateEventReviewPayload {
  eventId: number;
  ratings?: { [key: string]: number };
  textReview?: string;
  tags?: string[];
  isAnonymous: boolean;
}

export interface UserSettingsDto {
  allowReviews: boolean;
  showRatingsToOthers: boolean;
  showTextReviewsToOthers: boolean;
  allowAnonymousReviews: boolean;
  showPhotosToGuests: boolean;
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
  getUser: (id: string) => api.get<User>('/users/${id}'),
  getCurrentUser: () => api.get<User>('/users/me'),
  updateUser: (id: string, data: Partial<User>) => api.put<User>('/users/${id}', data),
};

export const reviewsAPI = {
  getReviews: () => api.get<Review[]>('/reviews'),
  getUserReviews: (userId: string) => api.get<Review[]>('/reviews/user / ${userId}'),
  createReview: (data: CreateUserReviewPayload) => api.post<Review>('/reviews', data),
};

export const eventsAPI = {
  getEvents: () => api.get<Event[]>('/events'),
  getEvent: (id: number) => api.get<Event>('/events/${id}'),
  createEvent: (data: { name: string; description?: string; date: string; location?: string; }) => api.post<Event>('/events', data),
  updateEvent: (id: number, data: Partial<Event>) => api.put<Event>('/events/${id}', data),
  deleteEvent: (id: number) => api.delete('/events/${id}'),
  joinEvent: (id: number) => api.post('/events/${id}/join'),
  leaveEvent: (id: number) => api.post('/events/${id}/leave'),
};

export const eventReviewsAPI = {
  getByEvent: (eventId: number) => api.get<EventReview[]>('/eventreviews/event/${eventId}'),
  create: (data: CreateEventReviewPayload) => api.post<EventReview>('/eventreviews', data),
};

export const reportsAPI = {
  create: (data: { targetType: 'Review' | 'Photo' | 'EventReview'; targetId: number; reason: string; description?: string }) =>
    api.post('/reports', data),
};

export const userSettingsAPI = {
  getMine: () => api.get('/usersettings/me'),
  updateMine: (data: {
    allowReviews: boolean;
    showRatingsToOthers: boolean;
    showTextReviewsToOthers: boolean;
    allowAnonymousReviews: boolean;
    showPhotosToGuests: boolean;
  }) => api.put('/usersettings/me', data),
};

export const adminRolesAPI = {
  getRoles: () => api.get<{ id: string; name: string; permissions: string[] }[]>('/admin/roles'),
  getAllPermissions: () => api.get<string[]>('/admin/roles/permissions'),
  syncRoles: () => api.post('/admin/roles/sync', {}),
  assignRole: (userId: string, role: string) => api.post(`/admin/roles/users/${userId}/assign`, { role }),
  revokeRole: (userId: string, role: string) => api.post(`/admin/roles/users/${userId}/revoke`, { role }),
  getUserRoles: (userId: string) => api.get<{ userId: string; roles: string[]; permissions: string[] }>(`/admin/roles/users/${userId}`),
};

export default api;