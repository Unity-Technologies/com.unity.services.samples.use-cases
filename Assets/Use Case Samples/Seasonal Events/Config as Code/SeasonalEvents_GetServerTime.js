// Entry point for the Cloud Code script 
module.exports = async ({ params, context, logger }) => {
  return Date.now();
}

module.exports.params = {};
