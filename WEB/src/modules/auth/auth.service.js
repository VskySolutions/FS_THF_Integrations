import { http2 } from "boot/axios";

export default {
  login (model) {
    return http2.post("/auth/login", model).then(response => response.data);
  },
  register (model) {
    return http2.post("/auth/register", model).then(response => response.data);
  },

  forgotPassword (model) {
    return http2.post("/auth/forgot-password", model).then(response => response.data);
  },

  resetPassword (model) {
    return http2.post("/auth/reset-password", model).then(response => response.data);
  },
  getUser (userid) {
    return http2.get(`/auth/user/?userid=${userid}`).then(response => response.data);
  }
};
