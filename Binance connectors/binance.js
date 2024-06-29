//not used if package imported, so this and all other files won't do anything here
module.exports = {
    BinanceRest: require('./rest.js'),
    BinanceWS: require('./ws.js'),
    ValueProcessor: require('./processor.js')
};
