## Railway Oriented Programming Library

### Reason
This is a library to support Railway Oriented Programming(ROP) in C# for Results and Errors handling under this pattern, mixing the best of Functional Programming paradigm while in a Object Oriented Programming paradigm.
The usage of this pattern results in a very straight forward and readable code easir to debbug and mantain within a results propagation context and the respective errors handling.
The pattern is composed by Result class that in its most basic representation contains the result value and the list of erros. Then the Switching behaviours between railways and the definitions of sucess and failure in the Results.