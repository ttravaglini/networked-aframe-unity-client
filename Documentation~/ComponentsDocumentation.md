# Documentation
The following documentation details how each component within networked-aframe-unity-client (NAF-unity-client) works, how it interacts with networked-aframe (NAF), and how you can get this library working within your own Unity project.

Before continuing, I highly recommend reading through the [networked-aframe documentation](https://github.com/networked-aframe/networked-aframe). Using this library assumes some level of familiarity with what networked-aframe is and how it works.

## How It Works
For a typical networked-aframe (NAF) experience, the way it works is that there is an A-Frame web app running in the browser, and that A-Frame app includes NAF components. Each NAF enabled web app communicates updates to a NAF enabled server, and the server broadcasts those updates out to each NAF web app using WebSockets. 

![networked-aframe client-server diagram](images/client_server_diagram.png)

In the NAF-unity-client scenario, the setup is largely the same. There is still a NAF-enabled WebSocket server, and each A-Frame web app is still communicating with it using WebSockets. The only difference in this scenario is that there is also a Unity client that is connecting to the NAF WebSocket server. That Unity client is reading the messages being broadcast from the other web apps, and it is broadcasting messages of its own, communicating updates for the networked objects being generated from the Unity client.

![networked-aframe client-server diagram with Unity](images/Unity_client_server_diagram.png)

## Key Components
The following section reviews the key components/classes in the NAF-unity-client library and their corresponding components in networked-aframe.

### NAFScene
The `NAFScene` class defines the key properties required to establish the connection to the NAF server, and it facilitates the communication with and coordination of networked entities with the server and other clients. The `NAFScene` in Unity is analagous to the `<a-scene>` component [defined in networked-aframe](https://github.com/networked-aframe/networked-aframe#scene-component). 


There are a number of properties that need to be defined for a NAFScene. These properties should generally align with the same properties defined in the NAF `<a-scene>` component.

| Property name | Description | Default Value |
| --- | --- | ---|
| Server Url | The URL for the networked-aframe server that the Unity client will connect to | http://localhost:8080 |
| Room To Join | Unique room name. Can be multiple per app. Spaces are not allowed. There can be multiple rooms per app and clients can only connect to clients in the same app & room.	| dev |
| Template Prefabs | Specify the Unity objects that will be created for networked entities. For example, if an A-Frame browser client creates a new networked object, the Unity app will use one of the templates in this list to create that networked object within the Unity app. | [] |
| Network Updates Per Second | How many updates per second the Unity client will send to the NAF server | 15 |
| Naf Adapter | Defines which network adapter to use. Needs to align with the network type for the NAF server. Only available option at this point is the `WsEasyRTCAdapter` | N/A |

As an example, see the `WSClient` Game Object in the ["Basic Events" sample scene](../Samples~/NafSamples/Scenes).


### Network Adapters
Networked-aframe supports the choice of multiple different types of network connections that can be used to handle the networking between the various clients. For example, with networked-aframe, you can use either WebSockets or WebRTC protocols, and you have multiple variants of network libraries to use that support those protocols. [See the full list here](https://github.com/networked-aframe/networked-aframe#adapters). 

For this NAF-unity-client library, there is currently only a single option for the network adapter, which is the `WsEasyRTCAdapter`. This corresponds to the `wseasyrtc` adapter used by networked-aframe, which uses WebSockets as the protocol for the network communication.

To use an adapter with NAF-unity-client, attach the `WsEasyRTCAdapter` script to a GameObject, and then reference that game object in the `Naf Adapter` property of the `NAFScene`. 

As an example, see the `WSClient` Game Object in the ["Basic Events" sample scene](../Samples~/NafSamples/Scenes).

### NAFTemplate
Networked-aframe [has the concept of](https://github.com/networked-aframe/networked-aframe#creating-networked-entities) `<template>` objects, and that functionality is replicated using the `NAFTemplate` class in NAF-unity-client. The purpose of a template is to define which object should be created when a networked client (a.k.a. a browser or app that is not the local user's app) creates a new networked object. When the other client creates the object, NAF-unity-client will use the object with the correct NAFTemplate to create the corresponding object in the Unity app. The way this works is using the `TemplateId` property on the NAFTemplate. This `TemplateId` property should match with the `id` attribute on the `<template>` tag in the networked-aframe app. The Unity Prefab with the `NAFTemplate` script attached is then added to the `NAFScene` under the `Template Prefabs` property. So, when the `NAFScene` receives a message from the networked-aframe server saying that a new networked object should be created, the NAFScene logic looks in the list of `Template Prefabs` for the correct `NAFTemplate` object to instantiate by matching the `TemplateId` on that object to the `id` attribute received as part of the message.

As an example, see the `WSClient` Game Object in the ["Basic Events" sample scene](../Samples~/NafSamples/Scenes) and the ["AvatarTemplate" prefab](../Samples~/NafSamples/Prefabs).

#### Properties
| Property name | Description | Default Value |
| --- | --- | --- |
| TemplateId | The string value that should correspond to the `id` of the `<template>` used in the networked-aframe app. This value should **not** include the leading `#` character. | N/A |
| Custom Component Parsers | List of `CustomComponentParser` components required for this Template. Refer to the [Custom Component Parsers](#custom-component-parsers) section | []

#### Custom Component Parsers
By default with networked-aframe, the position and rotation of networked objects are sent to all clients for a given NAF scene. Scene authors also have the ability to sync other attributes for networked objects, which is [done in networked-aframe](https://github.com/networked-aframe/networked-aframe#syncing-custom-components) by defining custom schemas on the `template` object.

In this library, that functionality is recreated using the `CustomComponentParser` abstract class. This class has two abstract methods to be defined for any concrete instance: 

 - `ParseCustomComponentValue`: This method is used to parse the custom component value that is being sent as part of a networked entity from a non-local client. For example, in the [ColorCustomComponentParser](../Samples~/NafSamples/ColorCustomComponentParser.cs), this method receives a hex code from a network message, and it then parses that hex code and adds the appropriate color to the networked object rendered in Unity.
 - `GenerateCustomComponentValue`: This method is used to determine what value should be sent to the NAF server by Unity when Unity sends a network update for this Game Object. For example, in the [ColorCustomComponentParser](../Samples~/NafSamples/ColorCustomComponentParser.cs), this method retrieves the color for the attached GameObject, translates that color into a hex code, and then sends that hex code as part of the network update to the NAF server.

 This class also defines the public property `ComponentIndex`. This property is one of the more confusing aspects of the NAF-unity-client library, so it requires a bit of explanation. As described above, the Unity app receives a WebSocket message from the networked-aframe server which contains the information about when & how a custom component value should be updated. However, the issue is that the message that networked-aframe sends does not include any information about **which** custom component value needs to be updated other than an `index` value. This `index` is determined by the order in which the custom components are added to the custom schema on the `template` object inside of the networked-aframe app. Generally, you should assume that the default `position` and `rotation` components will occupy indices `0` and `1`, so any custom components will start at `ComponentIndex = 2` and then increment by one as new custom components are added.

### Networked Entities
The `NetworkedEntity` class in this library is used to sync the component values for objects across the network to connected clients. Inside your Unity app, if you want to sync an object with other clients, then you'll need to do a few things:

1. In your networked-aframe web app, define your `<template>` object, which will be used by NAF to render the object within the web app
2. Attach the `NAFTemplate` script to the desired GameObject in Unity, and set the `TemplateId` property to match the `id` of the corresponding `<template>` from the NAF web app
3. Attach the `NetworkedEntity` script to the Unity GameObject, and add a reference to the appropriate GameObject with the NAFScene attached in the networked entity's `Naf Scene` parameter.

As an example, see the `LocalPlayerToSync` GameObject in the ["Basic Events" sample scene](../Samples~/NafSamples/Scenes).