'use strict';
var BitMEXClient = require('../index');

//testnet:true does not seem to work....
var client = new BitMEXClient({ testnet: false, maxTableLen: 1000 }); ///1000 elements is about half a minutes's worth of data, and anything up to 400 elements may be used

var s = "connectionopened";
client.on('open', () => console.log(JSON.stringify({ message: "Connection opened" })));
client.on('close', () => console.log(JSON.stringify({ message: "Connection closed" })));
client.on('initialize', () => console.log(JSON.stringify({ message: "Initialized, data is flowing" })));

client.addStream('XBTUSD', 'quote', function(data, symbol, table) {

    var toJSONobject = {
        symbol: symbol,
        table: table,
        data: data
    };
    console.log(JSON.stringify(toJSONobject));
});

client.on('error', function(e) {

    console.log(JSON.stringify({
        error: e
    }));
});

//all other functions are done with GET and POST requests.
//communicate with the node.js process as for Binance, to send commands, etc.
//for examples of how to do order post requests, see https://github.com/BitMEX/api-connectors/tree/master/official-http
//also: https://www.bitmex.com/app/restAPI
//also: https://www.bitmex.com/api/explorer/#!/Order/Order_new
//https://github.com/binance-exchange/binance-official-api-docs
//https://github.com/binance-exchange/binance-official-api-docs/blob/master/rest-api.md?spm=a2c6h.14275010.0.0.73877c0bWCnPK4#signed-trade-and-user_data-endpoint-security