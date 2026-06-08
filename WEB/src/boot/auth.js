import { boot } from "quasar/wrappers";
import { LocalStorage } from "quasar";
import { useAuthStore } from "stores/auth";

export default boot(({ router, store }) => {
  router.beforeEach((to, from, next) => {
    if (to.matched.some(record => record.meta.requiresAuth)) {
      const token = LocalStorage.getItem("token");

      if (!token) {
        next({ name: "login" });
      } else {
        const authStore = useAuthStore(store);
        const user = authStore.user;
        const administrator = (user != null && user.roles ? user.roles.indexOf("superadmin") > -1 : false);

        if (to.matched.some(record => record.meta.requiresAdmin) && !administrator) {
          next({ name: "not_authorized" });
        } else {
          next();
        }
      }
    } else {
      next();
    }
  });
});
