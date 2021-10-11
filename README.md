## What is this?
Basically, Mr.Beast ran a contest called [Finger On The App 2](https://apps.apple.com/us/app/finger-on-the-app-2/id1527896667) in March of 2021 and I wanted to see if I could use my 3D Printer to try and cheat for fun. This was just for fun and I did not win any money off of this.

## How does it work? (hardware)
I bought a touch pen on Amazon and then quickly modeled a pen holder on Tinkercad to hold the pen. I also modified a model on Thingiverse to hold the phone that will actually be playing the game. [Here is a video](https://youtu.be/D-jZiT8pq8U) showing how the hardware setup looked like.

## How does it work? (software)
![image](https://user-images.githubusercontent.com/1060681/136858489-07ae7dd2-f42d-4bd0-a83f-ee57a2351e0d.png)

BASICALLY, there is a websocket server that accepts a pair of X,Y cordionates and calls the [Octoprint REST API](https://docs.octoprint.org/en/master/api/index.html) which I have setup on my 3D Printer. This allows the printer to accept arbitrary GCODE commands to move the printhead that holds the pen. 

The websocket server is also screen capturing the Android device via [scrcpy](https://github.com/Genymobile/scrcpy), encodes the image, and sends it over the websocket to the connected clients.

Unfortunately, I didn't end up saving my Web client code and it has been lost, but the Android client was saved. In the Web client it just sent the raw x,y position to the server and in the Android client it scaled the current screen resolution to the actual phone resolution. 

I have a video showing the [Web client](https://youtu.be/TGT1BXwF85w) and also the [Android client](https://youtu.be/s2CzpWgXEWQ) in action if you want to see the final result. I was moving my finger slowly in the Android demo because I was testing how noticable the delay was, I could have moved my finger as fast as I wanted though.
