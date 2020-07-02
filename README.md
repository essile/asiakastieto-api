# Asiakastieto API Azure function

This function demonstrates how to get data out from Asiakastieto api 
(as described on ["Introduction to company information API"](https://www.asiakastieto.fi/web/en/frontpage/integrations/introduction-to-integration?avp=prb)) 

_The Asiakastieto documentation was a bit complicated so hopefully this helps somebody :)_

To be able to use the api you need to [get credentials](https://www.asiakastieto.fi/web/fi/etusivu/rajapinnat/demo.html).

When the function is called, the following details should be given as headers:

- UserId* - from Asiakastieto (12 numbers)
- Password* - from Asiakastieto
- CheckSumKey* - from Asiakastieto
- SearchTerm* (Search term. On demo "Asiakastieto" or f.ex. part of the company name as "Asiakast")
- Language (FI, EN or SV. If header missing, EN by default)
- Mode (demo or prod. If header missing, demo by default. It kind of seems prod version does not work with test credentials)

(*required)

To get data from the api the function automatically creates the hash needed using Finnish time (offset +02).

The hash is created by combining userid, enduser, timestamp (yyyyMMddHHmmssff + offset + consecutive number) and checksumkey, hashing the combination with SHA-512 and converting it back to string.

Returns the response from Asiakastieto as xml.
