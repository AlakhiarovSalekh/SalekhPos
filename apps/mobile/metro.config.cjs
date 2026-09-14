const path = require("node:path");
const { getDefaultConfig } = require("expo/metro-config");

const config = getDefaultConfig(__dirname);
const apiClientSource = `${path.resolve(__dirname, "../../packages/api-client/src")}${path.sep}`;

config.resolver.resolveRequest = (context, moduleName, platform) => {
  if (
    context.originModulePath.startsWith(apiClientSource) &&
    moduleName.startsWith(".") &&
    moduleName.endsWith(".js")
  ) {
    return context.resolveRequest(context, `${moduleName.slice(0, -3)}.ts`, platform);
  }
  return context.resolveRequest(context, moduleName, platform);
};

module.exports = config;
