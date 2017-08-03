# bikeVRconcepts

AforgeLEDAngleFinder
This app was designed to use a webcam to find the angle displayed by a set of LED lights that were going to be attached to a bicycle pedal.
The app detects 3 bright objects in the camera's view and assumes they're the LED lights.  It then sorts the LEDS by distance between one
another.  The two closeset LEDs are considered the Base of a triangle while the remaining further away LED is the height of it.  
The midpoint between the first 2 LEDs and the third LED's position is then used as a Vector which is converted into an absolute angle via the
law of cosines.

This angle is then converted to a speed, which would be sent to the VR bike game which connects to the socket that this app opens.
