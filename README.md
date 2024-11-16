
# Redis Reverse Proxy C# - MultiThreaded Server

I Recently was playing with HttpListener and was curios about how things work behind the scene.
one of my friends had a problem that iis could not handle large amount of requests and they had loss in the requests, i worked on this Reverse Proxy to handle large requests. the proccess is like this :

- get all the requests
- store them in Redis
- send the requests to the Target server with a specified batch size
- remove the sended requests from Redis

## Features

- RedisReverseProxy listen to a port and store requests
- RequestWorker for sending requests to target server
- Redis for data management
- MultiThreading for handling each request in a seperate thread and for sending to the target server

