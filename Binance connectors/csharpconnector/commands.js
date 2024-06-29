//files are used to create modules, one per file
//this constructs the module from the file:
//const SomeModule = require('./commands.js');

//to construct an object from the module code:
//var c1 = new SomeModule(); - not using the actual class name within the module
//need to export the object from the module: module.exports = BinanceCommands
// - this will not allow access of the exported object from the export name, if this export syntax is used 
//But if specific object is assigned to this class, then can access it from the object, e.g. 
//module.exports = {
//   BinanceObject: BinanceCommands
//};
//now can access by: var c1 = new SomeModule.BinanceObject()
//then can add other objects, functions etc. in this module and export those the same way
//also, could just do this as:
//const BinanceCommandExecute = (data) => {
    // whatever
//};

class BinanceCommands {

    constructor(binanceRest, binanceWS)
    {
        this.binanceRest = binanceRest;
        this.binanceWS = binanceWS;
    }

    execute(JsonData) {
        try {

            var operation = JsonData.Operation;
            var data = JsonData.Data;
            //var something = JsonData.something; //this will just be undefined so won't throw an exception

            //      var obj = { "Message": "Received: " + operation };
            //console.log(JSON.stringify(obj));
            //console.log(JSON.stringify({ "Hello": "World" }));

            //this will output the error message and code:
            //console.error(data1.msg + ", " + data1.code);

            //this would be the instruction to set trade, exit etc. Input is a text string so could json-encode
            var msg = "";

            //the convention here is to pass the rest.js method name in as the operation parameter (camel case), use that in the switch statement, and return the same name in the response
            //there must be a property in BinanceCaptureData which matches the operation parameter, to receive the returned JSON-decoded data
            switch (operation) {

                case "ExchangeInfo":
                    this.binanceRest.exchangeInfo((err, data1) => {
                        if (err) {
                            console.error(data1.msg + ", " + data1.code);
                        } else {
                            console.log(JSON.stringify({ "ExchangeInfo": JSON.stringify(data1) }));
                        }
                    });
                    break;

                case "Account":

                    this.binanceRest.account((err, data1) => {
                        if (err) {
                            console.error(data1.msg + ", " + data1.code);
                        } else {
                            console.log(JSON.stringify({ "Account": JSON.stringify(data1) }));
                        }
                    });
                    break;

                case "QuotePrice":
                    
                    this.binanceRest.tickerPrice(data, (err, data1) => {
                        if (err) {
                            console.error(data1.msg + ", " + data1.code);
                        } else {
                            console.log(JSON.stringify({ "QuotePrice": JSON.stringify(data1) }));
                        }
                    });
                    break;

                case "AllOrders":
                    this.binanceRest.allOrders(data, (err, data1) => {
                        if (err) {
                            console.error(data1.msg + ", " + data1.code);
                        } else {
                            console.log(JSON.stringify({ "AllOrders": JSON.stringify(data1) }));
                        }
                    });
                    break;

                case "QueryOrder":
                    this.binanceRest.queryOrder(data, (err, data1) => {
                        if (err) {
                            console.error(data1.msg + ", " + data1.code);
                        } else {
                            console.log(JSON.stringify({ "QueryOrder": JSON.stringify(data1) }));
                        }
                    });
                    break;

                case "NewOrder":
                    this.binanceRest.newOrder(data, (err, data1) => {
                        if (err) {
                            console.error(data1.msg + ", " + data1.code);
                        } else {
                            console.log(JSON.stringify({ "NewOrder": JSON.stringify(data1) }));
                        }
                    });
                    break;

                case "NewOCO":
                    //console.log(JSON.stringify({ "Message": "Received" }));
                    //return;
                    this.binanceRest.newOCO(data, (err, data1) => {
                        if (err) {
                            console.error(data1.msg + ", " + data1.code);
                        } else {
                            console.log(JSON.stringify({ "NewOCO": JSON.stringify(data1) }));
                        }
                    });
                    break;

                case "CancelOrder":
                    this.binanceRest.cancelOrder(data, (err, data1) => {
                        if (err) {
                            console.error(data1.msg + ", " + data1.code);
                        } else {
                            console.log(JSON.stringify({ "CancelOrder": JSON.stringify(data1) }));
                        }
                    });
                    break;

                case "OpenOrders":
                    this.binanceRest.openOrders(data, (err, data1) => {
                        if (err) {
                            console.error(data1.msg + ", " + data1.code);
                        } else {
                            console.log(JSON.stringify({ "OpenOrders": JSON.stringify(data1) }));
                        }
                    });
                    break;

                case "MyTrades":              

                    this.binanceRest.allOrders(data, (err, data1) => {
                        if (err) {
                            console.error(data1.msg + ", " + data1.code);
                        } else {
                            console.log(JSON.stringify({ "MyTrades": JSON.stringify(data1) }));
                        }
                    });
                   
                    
                    break;

                case "SetSymbols":
                    var arr = [];
                    Object.values(data).forEach(val => {
                        arr.push(this.binanceWS.streams.kline(val, '1m'));
                    });

                    this.binanceWS.onCombinedStream(
                        arr,
                        streamEvent => {
                            try {
                                let data1 = JSON.stringify(streamEvent.data); //could remove the JSON.parse() call from ws._setupWebSocket() and work with the single-letter identifiers, if this needed to be more efficient

                                Object.values(data).forEach(val => {
                                    if (streamEvent.stream === this.binanceWS.streams.kline(val, '1m')) {
                                        console.log(data1);
                                    }
                                });
                            }
                            catch (err) {
                                console.log(JSON.stringify({ "Error": err.toString() }));
                            }
                        }

                    );
                    //console.log(JSON.stringify({ "Message": "Setup complete" }));
                    break;
                default:

            }
        }
        catch (error)
        {
            console.log(JSON.stringify({ "Error: ": err.toString() }));
        }


        //submit order, there is also a test order function as well
        /*
        var orderObject = {
            //order data here, formatted correctly
        };
        this.binanceRest.newOrder(orderObject, (err, data) => {
            if (err) {
                console.error(err);
            } else {
                console.log(data);
            }
        });
        */
    }



}


module.exports = {
    BinanceObject: BinanceCommands
};

