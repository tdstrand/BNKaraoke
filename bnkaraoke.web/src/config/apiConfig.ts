const API_BASE_URL = process.env.REACT_APP_API_URL || "https://localhost:7280";

export const API_ROUTES = {
  LOGIN: `${API_BASE_URL}/api/auth/login`,
  REGISTER: `${API_BASE_URL}/api/auth/register`,
  USER_ROLES: `${API_BASE_URL}/api/auth/roles`,
};

export default API_BASE_URL;
