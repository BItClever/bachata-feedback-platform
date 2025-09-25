import axios from 'axios';

// 1) Умное определение API_BASE_URL
//    Порядок приоритета:
//    - явный REACT_APP_API_URL из .env(.local)
//    - если домен фронта *.alexei.site — используем api-bachata.alexei.site
//    - локалка: http://localhost:5000/api
function resolveApiBaseUrl(): string {
  const envUrl = process.env.REACT_APP_API_URL?.trim();
  if (envUrl) return envUrl;

  const host = window.location.host.toLowerCase();

  // Авто‑угадать для домена под CF Tunnel,
  // чтобы прод не зависел от .env, а локалка — от дефолта:
  if (host.endsWith('alexei.site')) {
    return 'https://api-bachata.alexei.site/api';
  }

  // Локальная разработка
  if (host.startsWith('localhost') || host.startsWith('127.0.0.1')) {
    return 'http://localhost:5000/api';
  }

  // Жёстко упасть, чтобы не выстрелить себе в ногу в незнакомой среде
  throw new Error(
    'API base URL is not configured. Set REACT_APP_API_URL in .env(.local) or add host mapping in api.ts.'
  );
}

const API_BASE_URL = resolveApiBaseUrl();

// Для отладки — сразу видно, куда реально пойдут запросы
(window as any).__API_BASE_URL = API_BASE_URL;
console.log('[api] BASE_URL =', API_BASE_URL);

// Создаем экземпляр axios
const api = axios.create({
  baseURL: API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
});

// Интерцептор: добавляем токен
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    if (token) {
      (config.headers as any).Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Интерцептор: обработка ответов/ошибок
api.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error?.response?.status;
    const url = error?.config?.url || '';
    if (status === 401) {
      const token = localStorage.getItem('token');
      // 401 считаем критичным только для защищённых эндпоинтов
      const critical = ['/auth/me', '/users/me', '/reviews', '/events', '/userphotos', '/usersettings'];
      const isCritical = critical.some(path => url.includes(path));
      if (token && isCritical) {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
        window.location.href = '/login';
        return;
      }
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
  updatedAt?: string;
  dancerRole?: string; // "Lead" | "Follow" | "Both"
  roles?: string[];
  permissions?: string[];
  mainPhotoSmallUrl?: string;
  mainPhotoMediumUrl?: string;
  mainPhotoLargeUrl?: string;
  reviewsReceivedCount?: number;
  avgRating?: number | null;
  avgRatingUnique?: number | null;
  mainPhotoFocusX?: number | null;
  mainPhotoFocusY?: number | null;
}

export interface Event {
  id: number;
  name: string;
  description: string;
  date: string;
  location: string;
  createdBy: string;
  createdAt: string;
  updatedAt?: string;
  participantCount?: number;
  creatorName?: string;
  isUserParticipating?: boolean;
  coverImageSmallUrl?: string;
  coverImageLargeUrl?: string;
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
  moderationLevel?: 'Pending' | 'Green' | 'Yellow' | 'Red';
  moderationSource?: 'LLM' | 'Manual' | 'None';
  moderatedAt?: string;
  moderationReason?: string;
  moderationReasonRu?: string;
  moderationReasonEn?: string;
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
  moderationLevel?: 'Pending' | 'Green' | 'Yellow' | 'Red';
  moderationSource?: 'LLM' | 'Manual' | 'None';
  moderatedAt?: string;
  moderationReason?: string;
  moderationReasonRu?: string;
  moderationReasonEn?: string;
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
  getUser: (id: string) => api.get<User>(`/users/${id}`),
  getCurrentUser: () => api.get<User>('/users/me'),
  updateUser: (id: string, data: Partial<User>) => api.put<User>(`/users/${id}`, data),
};

export const reviewsAPI = {
  getReviews: () => api.get<Review[]>('/reviews'),
  getUserReviews: (userId: string) => api.get<Review[]>(`/reviews/user/${userId}`),
  getMyGiven: () => api.get<Review[]>('/reviews/mine/given'),
  createReview: (data: CreateUserReviewPayload) => api.post<Review>('/reviews', data),
};

export const eventsAPI = {
  getEvents: () => api.get<Event[]>('/events'),
  getEvent: (id: number) => api.get<Event>(`/events/${id}`),
  createEvent: (data: { name: string; description?: string; date: string; location?: string; }) => api.post<Event>('/events', data),
  updateEvent: (id: number, data: Partial<Event>) => api.put<Event>(`/events/${id}`, data),
  deleteEvent: (id: number) => api.delete(`/events/${id}`),
  joinEvent: (id: number) => api.post(`/events/${id}/join`),
  leaveEvent: (id: number) => api.post(`/events/${id}/leave`),
};

export const eventReviewsAPI = {
  getByEvent: (eventId: number) => api.get<EventReview[]>(`/eventreviews/event/${eventId}`),
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
  getUserRoles: (userId: string) =>
    api.get<{ userId: string; roles: string[]; permissions: string[] }>(`/admin/roles/users/${userId}`),
};

export const userPhotosAPI = {
  getMine: () =>
    api.get<{ id: number; isMain: boolean; smallUrl: string; mediumUrl: string; largeUrl: string; focusX?: number; focusY?: number }[]>(
      '/userphotos/me'
    ),
  getUser: (userId: string) =>
    api.get<{ id: number; isMain: boolean; smallUrl: string; mediumUrl: string; largeUrl: string; focusX?: number; focusY?: number }[]>(
      `/userphotos/user/${userId}`
    ),
  uploadMyPhoto: (file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return api.post('/userphotos/me/upload', fd, { headers: { 'Content-Type': 'multipart/form-data' } });
  },
  updateFocus: (photoId: number, focusX: number, focusY: number) =>
    api.patch(`/userphotos/${photoId}/focus`, { focusX, focusY }),
  setMain: (photoId: number) => api.post('/userphotos/me/set-main', { photoId }),
  delete: (photoId: number) => api.delete(`/userphotos/me/${photoId}`),
};

export const eventsAPIEx = {
  uploadCover: (eventId: number, file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return api.post(`/events/${eventId}/cover`, fd, { headers: { 'Content-Type': 'multipart/form-data' } });
  },
};

export const moderationAdminAPI = {
  getJobs: () => api.get('/admin/moderation/jobs'),
  requeue: (targetType: 'Review' | 'EventReview', targetId: number) =>
    api.post('/admin/moderation/requeue', { targetType, targetId }),
  setReviewLevel: (id: number, level: 'Green' | 'Yellow' | 'Red', reason?: string, reasonRu?: string, reasonEn?: string) =>
    api.put(`/admin/moderation/reviews/${id}`, { level, reason, reasonRu, reasonEn }),
  setEventReviewLevel: (id: number, level: 'Green' | 'Yellow' | 'Red', reason?: string, reasonRu?: string, reasonEn?: string) =>
    api.put(`/admin/moderation/eventreviews/${id}`, { level, reason, reasonRu, reasonEn }),
  listUserReviews: (params: { status?: string; search?: string; skip?: number; take?: number }) =>
    api.get('/admin/moderation/reviews/list', { params }),
  listEventReviews: (params: { status?: string; search?: string; skip?: number; take?: number }) =>
    api.get('/admin/moderation/eventreviews/list', { params }),
  getReportsByTarget: (targetType: 'Review' | 'EventReview' | 'Photo', targetId: number, status?: string) =>
    api.get('/admin/moderation/reports/by-target', { params: { targetType, targetId, status } }),
};

export const statsAPI = {
  get: () => api.get<{ totalUsers: number; totalReviews: number; totalEventReviews: number; totalEvents: number }>('/stats'),
};

export const usersAPIEx = {
  getUsersPaged: (params: { page?: number; pageSize?: number; search?: string }) =>
    api.get<{ items: User[]; total: number; page: number; pageSize: number; totalPages: number }>(
      '/users/paged',
      { params }
    ),
};

export const eventsAPIEx2 = {
  getEventsPaged: (params: { page?: number; pageSize?: number; search?: string }) =>
    api.get<{ items: Event[]; total: number; page: number; pageSize: number; totalPages: number }>(
      '/events/paged',
      { params }
    ),
};

export const eventPhotosAPI = {
  list: (eventId: number) =>
    api.get<{ id: number; smallUrl: string; mediumUrl: string; largeUrl: string; uploadedAt: string }[]>(
      `/events/${eventId}/photos`
    ),
  upload: (eventId: number, file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return api.post(`/events/${eventId}/photos`, fd, { headers: { 'Content-Type': 'multipart/form-data' } });
  },
  uploadMany: (eventId: number, files: FileList | File[]) => {
    const fd = new FormData();
    Array.from(files).forEach(f => fd.append('files', f));
    return api.post(`/events/${eventId}/photos`, fd, { headers: { 'Content-Type': 'multipart/form-data' } });
  },
  delete: (eventId: number, photoId: number) =>
    api.delete(`/events/${eventId}/photos/${photoId}`),
};

export default api;