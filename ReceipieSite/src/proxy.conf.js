const PROXY_CONFIG = [
  {
    context: [
      "/",
    ],
    target: "https://localhost:7087",
    secure: false,
    loglevel: "debug",
    changeOrigin: true
  }
]

module.exports = PROXY_CONFIG;
