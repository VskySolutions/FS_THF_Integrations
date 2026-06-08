import { boot } from "quasar/wrappers";
import { useTitle } from "@vueuse/core";

export default boot(({ app, router }) => {
  router.afterEach((to, from) => {
    useTitle(to.meta.title, { titleTemplate: "%s - THF Integration" });
  });
});
