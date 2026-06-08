import { http } from "boot/axios";

export default {
  getProfile () {
    return http.get("/account/profile").then(response => response.data);
  },

  // saveProfile (id, model) {
  //   if (id) {
  //     return http.put(`/account/${id}`, model).then(response => response.data);
  //   } else {
  //     return http.post("/account/profile", model).then(response => response.data);
  //   }
  // },

  saveProfile (model) {
    return http.post("/account/update-profile", model, { headers: { "Content-Type": "multipart/form-data" } }).then(response => response.data);
  },

  changePassword (model) {
    return http.post("/account/change-password", model).then(response => response.data);
  }
};
