import { boot } from "quasar/wrappers";
import { notifyError } from "assets/utils";
import { http2, http } from "boot/axios";
import { useAuthStore } from "stores/auth";

export default boot(({ store, router }) => {
  // request http2 axios interceptors (anonymous request)
  http2.interceptors.request.use(
    (config) => {
      return config;
    },
    (error) => {
      notifyError({
        timeout: 10000,
        message: getErrorMessage(error)
      });
      return Promise.reject(error);
    }
  );

  // response http2 axios interceptors (anonymous response)
  http2.interceptors.response.use(
    (response) => {
      return response;
    },
    (error) => {
      notifyError({
        timeout: 10000,
        message: getErrorMessage(error)
      });
      return Promise.reject(error);
    }
  );

  // request http axios interceptors
  http.interceptors.request.use(
    (config) => {
      return config;
    },
    (error) => {
      notifyError({
        timeout: 10000,
        message: getErrorMessage(error)
      });
      return Promise.reject(error);
    }
  );

  // response http axios interceptors
  http.interceptors.response.use(
    (response) => {
      return response;
    },
    (error) => {
      if (error.response && error.response.status === 401) {
        const authStore = useAuthStore(store);
        authStore.logout();
        router.push({ name: "login", params: {} });
        return Promise.reject(error);
      } else {
        notifyError({
          timeout: 10000,
          message: getErrorMessage(error)
        });
        return Promise.reject(error);
      }
    }
  );

  function getErrorMessage (error) {
    const errMgs = error?.response?.data?.message || (typeof error?.response?.data === "string" && error.response.data) || "An error occurred while processing your request.";
    console.error("Request Error:- " + errMgs);
    return errMgs;
  }
});
