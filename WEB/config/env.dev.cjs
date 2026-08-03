module.exports = {
  // Plain http: the API's only launch profile binds http://localhost:5032 (no dev https port/cert), so
  // calling it over https fails the TLS handshake outright — ERR_SSL_PROTOCOL_ERROR, not a 4xx/5xx.
  API_BASE_URL: "http://localhost:5032",
  WEB_BASE_URL: "http://localhost:9000",
  BUILD_PUBLIC_PATH: "",
  PUBLISH_FOLDER: "",
  IGNORE_PUBLIC_FOLDER: false
};
