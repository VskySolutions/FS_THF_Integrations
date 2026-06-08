import { defineStore } from "pinia";
import { LocalStorage } from "quasar";
import authService from "modules/auth/auth.service";

export const useAuthStore = defineStore("auth", {

  state: () => ({
    token: LocalStorage.getItem("token"),
    user: LocalStorage.getItem("user")
  }),

  getters: {
    loggedIn: (state) => !!state.token
  },

  actions: {

    async login (model) {
      try {
        const resp = await authService.login(model);

        if (resp.token) {
          LocalStorage.clear();

          const token = resp.token;

          const user = {
            employeeId: resp.employeeId,
            userId: resp.userId,
            personId: resp.personId,
            username: resp.username,
            firstName: resp.firstName,
            lastName: resp.lastName,
            email: resp.email,
            userEmail: resp.userEmail,
            roles: resp.roles,
            siteId: resp.siteId,
            siteName: resp.siteName,
            siteTimeZone: resp.siteTimeZone,
            siteLandingPageLink: resp.siteLandingPageLink
          };

          this.token = token;
          this.user = user;

          LocalStorage.set("token", token);
          LocalStorage.set("user", user);

          return resp;
        }
      } catch (error) {
        console.error("Login failed:", error);
        throw error;
      }
    },

    logout () {
      LocalStorage.clear();

      this.token = null;
      this.user = null;
    },

    setUserInfo (payload) {
      this.user = {
        ...this.user,
        ...payload
      };

      LocalStorage.set("user", this.user);
    },

    changeTenant (siteId, siteTimeZone, name, landingPage) {
      const updatedUser = {
        ...this.user,
        siteId,
        siteTimeZone,
        siteName: name,
        siteLandingPageLink: landingPage
      };
      this.user = updatedUser;
      LocalStorage.set("user", updatedUser);
    }
  }
});
