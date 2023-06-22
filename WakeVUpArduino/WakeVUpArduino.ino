#include <Wire.h>
#define SLAVE_ADDRESS 0x40

const int numberOfSegments = 8;

const int segmentPins[numberOfSegments] = { 4, 7, 8, 6, 5, 3, 2, 9 };

const int numberOfValues = 4;

const int valuePins[numberOfValues] = { 10, 11, 12, 13 };

int values[numberOfValues] = { 0, 0, 0, 0 };

void setup()
{
  Serial.begin(9600);
  for(int i = 0; i < numberOfValues; ++i)
  {
    pinMode(valuePins[i], OUTPUT);
  }
  for(int i = 0; i < 8; ++i)
  {
    pinMode(segmentPins[i], OUTPUT);
  }

  Wire.begin(SLAVE_ADDRESS);
  Wire.onReceive(receiveTime);
}

void receiveTime(int numBytes)
{
  if(numBytes == 4)
  {
    for(int i = 0; i < 4; ++i)
    {
      values[i] = Wire.read();
    }
  }
}

void loop()
{
  showValues(values);
}

void showValues(int* values)
{
  for(int i = 0; i < numberOfValues; ++i)
  {
    showValue(values[i], i);
  }
}

void showValue(int value, int index)
{
  digitalWrite(valuePins[index], HIGH);
  for(int segment = 0; segment < numberOfSegments; ++segment)
  {
    boolean isBitSet = bitRead(value, segment);
    isBitSet = !isBitSet;
    digitalWrite(segmentPins[segment], isBitSet);
  }
  delay(5);
  digitalWrite(valuePins[index], LOW);
}
