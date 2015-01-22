# XdtExtended
Extended version of Microsoft's XML Document Transform (XDT) library

Original source: http://xdt.codeplex.com/

##Documentation
Usage for the core locators and transforms can be found here:
https://msdn.microsoft.com/en-us/library/dd465326(v=vs.110).aspx

### Applying Transforms Manually
```
public void ApplyTransform(string sourcePath, string transformPath, string resultPath)
{
  using (Stream stream = new FileStream(sourcePath, FileMode.Open))
  {
    using (var x = new XmlTransformation(transformPath))
    {
      var xmlDocument = new XmlDocument();
      xmlDocument.Load(stream);
      x.Apply(xmlDocument);
      xmlDocument.Save(resultPath);
    }
  }
}
```
###Transforms
#### xdt:Transform="InsertMultiple"
Adds the element that is defined in the transform file as a sibling to all selected elements. The new element is added at the end of any collection. Inspired by: http://stackoverflow.com/a/23294251/2836187
#####Example
Source:
```
<list>
  <item>
    <field/>
  </item>
  <item/>
  <item/>
</list>
```
Transform:
```
<list>
  <item>
    <property xct:Transform="InsertMultiple"/>
  </item>
</list>
```
Result:
```
<list>
  <item>
    <field/>
    <property/>
  </item>
  <item>
    <property/>
  </item>
  <item>
    <property/>
  </item>
</list>
```
###Other Documentation Resources and Features to Add
https://github.com/projectkudu/kudu/wiki/Xdt-transform-samples
http://sedodream.com/2010/09/09/ExtendingXMLWebconfigConfigTransformation.aspx
http://stackoverflow.com/a/3674169/2836187
https://github.com/sayedihashimi/XdtSample/blob/master/Program.cs

##License
Apache License 2.0
