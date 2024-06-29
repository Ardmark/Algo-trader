const api = require('binance'); //see folder for source files, although they are not used as they have been installed as a npm package
const binanceRest = new api.BinanceRest({
    //key: 'api-key', // Get this from your account on binance.com (this default value will work to read data though)
    //secret: 'api-secret', // Same for this

    key: 'vVo1RvHqDbC07mA7QTaj8Q7dtF6qE7mCrTBztdHOidQLml5dhdk4v1nLDwlzHyZ6', // also, should enable only trusted IP addresses on Binance - have not done this yet
    secret: 'H7IhdsS3TVUoTjowVOC93lGpcVZo5PjCD1I2vwUeWbIO02N1EUTeb21nWavZfrB1', // Same for this

    timeout: 15000, // Optional, defaults to 15000, is the request time out in milliseconds
    recvWindow: 10000, // Optional, defaults to 5000, increase if you're getting timestamp errors
    disableBeautification: false,
    /*
     * Optional, default is false. Binance's API returns objects with lots of one letter keys.  By
     * default those keys will be replaced with more descriptive, longer ones.
     */
    handleDrift: false,
    /*
     * Optional, default is false.  If turned on, the library will attempt to handle any drift of
     * your clock on it's own.  If a request fails due to drift, it'll attempt a fix by requesting
     * binance's server time, calculating the difference with your own clock, and then reattempting
     * the request.
     */
    baseUrl: 'https://api.binance.com/',
    /*
     * Optional, default is 'https://api.binance.com/'. Can be useful in case default url stops working.
     * In february 2018, Binance had a major outage and when service started to be up again, only
     * https://us.binance.com was working.
     */
    requestOptions: {}
    /*
     * Options as supported by the 'request' library
     * For a list of available options, see:
     * https://github.com/request/request
     */
});




/*
// You can use promises
binanceRest
    .allOrders({
        symbol: 'BNBBTC' // Object is transformed into a query string, timestamp is automatically added
    })
    .then(data => { //RW - the then() method returns a Promise
        console.log(data);
    })
    .catch(err => {
        console.error(err);
    });
    */
/*
 * Or you can provide a callback.  Also, instead of passing an object as the query, routes
 * that only mandate a symbol, or symbol and timestamp, can be passed a string.
 */
/*
binanceRest.allOrders('BNBBTC', (err, data) => {
    if (err) {
        console.error(err);
    } else {
        console.log(data);
    }
});
*/


/*
 * WebSocket API
 *
 * Each call to onXXXX initiates a new websocket for the specified route, and calls your callback with
 * the payload of each message received.  Each call to onXXXX returns the instance of the websocket
 * client if you want direct access(https://www.npmjs.com/package/ws).
 */
const binanceWS = new api.BinanceWS(true); // Argument specifies whether the responses should be beautified, defaults to true (beutification replaces the single id character with a readable string, e.g. 'c' becomes 'close'

//set up for processing of REST
const binanceCommands = require('./commands.js');
var cmds = new binanceCommands.BinanceObject(binanceRest, binanceWS);

//redirect stdin, this will receive the output from the C# process
const readline = require('readline');

const rl = readline.createInterface({
    input: process.stdin
});

//send this to the command processor
rl.on('line', (input) => {
    try {
        //    console.log(JSON.stringify({ "Message": input }));
        var data = JSON.parse(input);
        cmds.execute(data);
    }
    catch (ex) {
        console.log("Exception: " + ex);
    }
});

//all time signatures sent are in ms
const streams = binanceWS.streams;

//this returns a WebSocket instance. This could be set up dynamically, by closing the previous instance then changing the stream parameters, and calling method again
//otherwise will need to close the app down to change the symbols

/* The Websocket streams are User Data Streams. These are not used for sending data requests, setting trades etc.
 * 
 * These are the onXXXXX methods which are available. These mostly just call this._setupWebSocket(...) with various parameters, the last is this.streams.whatever
 * - so onTrade calls this.streams.trade(symbol), and these direct calls can be combined in onCombinedStreams(...) (see below)
onDepthUpdate
onDepthLevelUpdate
onKline
onAggTrade
onTrade
onTicker
onAllTickers
onUserData
onCombinedStream

//Use as follows:
//binanceWS.onDepthUpdate('BNBBTC', data => {
//    console.log(data);
//});

//binanceWS.onAggTrade('BNBBTC', data => {
//    console.log(data);
//});

//binanceWS.onKline('BNBBTC', '1m', data => {
//    console.log(data);
//});
*/

/*
 * You can use one websocket for multiple streams.  There are also helpers for the stream names, but the
 * documentation has all of the stream names should you want to specify them explicitly.
 */



//rw - regardless of whether this is done here or in the onKline method, only one line of data at a time is streamed, not the entire data set for the candle

/*


binanceWS.onCombinedStream(
    [

        streams.kline('BTCUSDC', '1m')
     //   streams.kline('ALGOBTC', '1m')
     //  streams.depth('BNBBTC'),
     //   streams.trade('BNBBTC'),
     //   streams.ticker('BNBBTC')
    ],
    streamEvent => {
        try {
            let data = JSON.stringify(streamEvent.data); //could remove the JSON.parse() call from ws._setupWebSocket() and work with the single-letter identifiers, if this needed to be more efficient
            switch (streamEvent.stream) {

                case streams.kline('BTCUSDC', '1m'): //fires when trade numbers change, price changes etc., with 'final' field set to true when period ends (60000 ms expired). Close is the current price at this time.
                case streams.kline('BNBBTC', '1m'):
                    console.log(data);
                    break;

                //       case streams.trade('BNBBTC'):
                //           console.log(
                //               'Trade event, update trade history\n',
                //               streamEvent.data
                //           );
                //           break;
                //       case streams.ticker('BNBBTC'): //this is a rolling 24 hour period - it does contain the close at the timestamp, and is streamed faster than the kline data set
                //           console.log(
                //               'Ticker event, update market stats\n',
                //               streamEvent.data
                //           );
                //           break;
                //       case streams.depth('BNBBTC'):
                //           console.log(
                //               'Depth event, update order book\n',
                //               streamEvent.data
                //           );
                //           break;

            }
        }
        catch { }

    }
           
);
 */



/*
 * onUserData requires an instance of BinanceRest in order to make the necessary startUserDataStream and
 * keepAliveUserDataStream calls.  The webSocket instance is returned by promise rather than directly
 * due to needing to request a listenKey from the server first.
 */

/*
binanceWS
    .onUserData(
        binanceRest,
        data => {
            console.log(data);
        },
        60000
    ) // Optional, how often the keep alive should be sent in milliseconds
    .then(ws => {
        // websocket instance available here
    });

*/