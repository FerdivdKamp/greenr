## Introduction to the greenr backend


To run the backend, when this is running you can start the mobile app (see the mobile app README).
```bash
dotnet run
```


### Swagger

Swagger is a set of tools and standards for documenting, testing, and interacting with RESTful APIs. It's now part of the broader OpenAPI Specification (OAS) ecosystem, but people often still say "Swagger" to refer to both the tools and the specification.

* Describes your API (endpoints, methods, parameters, responses).
* Generates interactive documentation so developers can try out the API in the browser.
* Makes integration easier by generating client/server code automatically (if needed).

You will the it's listening to one or more addresses, you can copy-paste either of the addresses and add /swagger to open the Swagger UI in the browser.


For example:
```
http://localhost:5285/swagger/index.html
```

