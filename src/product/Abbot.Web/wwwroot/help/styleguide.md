---
Title: Style Guide
---

# Style Reference

## Defaults
<span style="font-family: var(--brand-font)">The Abbot brand text uses [Bungee](https://fonts.google.com/specimen/Bungee).</span>

<span style="font-family: var(--body-font)">The base font for the website is [Roboto](https://fonts.google.com/specimen/Roboto).</span>

<code>Code is rendered using [Inconsolata](https://fonts.google.com/specimen/Inconsolata).</code>

### Site Color Variables:
 <ul class="color-list">
    <li>
        <div class="colorway" style="background-color: var(--primary-color)">&nbsp;</div> 
        --primary-color 
    </li>
    <li>
        <div class="colorway" style="background-color: var(--secondary-color)">&nbsp;</div>
        --secondary-color
    </li>
    <li>
        <div class="colorway" style="background-color: var(--background-color)">&nbsp;</div>
        --background-color
    </li>
    <li>
        <div class="colorway" style="background-color: var(--hover-color)">&nbsp;</div>
        --hover-color
    </li>
    <li>
        <div class="colorway" style="background-color: var(--selected-color)">&nbsp;</div>
        --selected-color
    </li>
    <li>
        <div class="colorway" style="background-color: var(--text-color)">&nbsp;</div>
        --text-color
    </li>
    <li>
        <div class="colorway" style="background-color: var(--boring-gray-color)">&nbsp;</div>
        --boring-gray-color
    </li>
    <li>
        <div class="colorway" style="background-color: var(--dark-gray-color)">&nbsp;</div>
        --dark-gray-color
    </li>
    <li>
        <div class="colorway" style="background-color: var(--success-color)">&nbsp;</div>
        --success-color
    </li>
    <li>
        <div class="colorway" style="background-color: var(--success-color-dark)">&nbsp;</div>
        --success-color-dark
    </li>
    <li> 
        <div class="colorway" style="background-color: var(--info-color)">&nbsp;</div>
        --info-color
    </li>
    <li>
        <div class="colorway" style="background-color: var(--info-color-dark)">&nbsp;</div>
        --info-color-dark
    </li>
    <li>
        <div class="colorway" style="background-color: var(--warning-color)">&nbsp;</div>
        --warning-color
    </li>
    <li>
        <div class="colorway" style="background-color: var(--warning-color-dark)">&nbsp;</div>
        --warning-color-dark
    </li>
    <li>
        <div class="colorway" style="background-color: var(--error-color)">&nbsp;</div> 
        --error-color
    </li>
    <li>
        <div class="colorway" style="background-color: var(--error-color-dark)">&nbsp;</div>
        --error-color-dark
    </li>

</ul>


## Typography (default styles)

# h1 looks like this
## h2 looks like this
### h3 looks like this
#### h4 looks like this
##### h5 looks like this

`code looks like this`

**bold looks like this**

_italics look like this_


### Lists (default styles)

An unordered list
  * Thing one
  * Thing two
  * Thing three
  
An ordered list
  1. Thing one
  2. Thing two
  3. Thing three

### Tables (default styles)

|Column 1|Column 2|Column 3|Column 4|
|___|___|___|___|
|Text Row One| Alpha|| Robot ipsum Auroer arnoes thermoite offistic cardiographer dasyty sulphureosphere.|
|Text Row Two| Bravo| <input type="checkbox" />| eleniostasis dactyllepry pathoxor quadtropic petaart chirogamy decgenesis|
|Text Row Three| Charlie| <input type="checkbox" selected />| Gymnoence hyloectomy piscicolous spondylogyne quadriphagy dichlorward valerophagous.|
|Text Row Four| Delta| <input type="radio" name="a_radio" selected />|  Hysterotomy anglothermic outone osmiomycete circumlysis|
|Text Row Five| Echo| <input type="radio" name="a_radio" />| Chrysothermic kiloarium spermatoent taxocene gymnopode hematolet hexacene Mcgram|

### Forms
<p>
    <label for="dat_textbox">Text boxes look like this: <input type="text" name="dat_textbox" /></label>
</p>
<p>
 <input type="checkbox"> This is a checkbox
</p>     
<p>
    <label for="dat_radio">Radio buttons: 
        <label><input type="radio" name="dat_radio" value="1">Option One</label>
        <label><input type="radio" name="dat_radio" value="2">Option Two</label>
    </label>
</p>
<p>
    <label for="dat_select">Select boxes look like this: 
    <select name="dat_select">
        <option>Option 1</option>
        <option>Option 2</option>
        <option>Option 3</option>
    </select>
    </label>
</p> 

### Buttons  
<p>
    <button class="btn">Default</button>
    <button class="btn btn-light">Light</button>
    <button class="btn btn-dark">Dark</button>
    <button class="btn btn-info">Info</button>
    <button class="btn btn-success">Success</button>
    <button class="btn btn-warning">Warning</button>
    <button class="btn btn-error">Error</button>
<p>

### Alerts
All alerts can be dismissed by clicking on them. Dismissal of an alert should never persist any information (use a button for this if you need). 

Alerts should render in a section called "Notifications" if created server side, or get appended to the `notifications` div in the page. 

<div class="alert">
    This is an alert. Use <code><strong>class="alert"</strong></code> for this.
</div>

<div class="alert alert-success">
    This is a <strong>success</strong> alert. Use <strong><code>class="alert alert-success"</code></strong> for this.
</div>

<div class="alert alert-info">
    This is an <strong>info</strong> alert. Use <strong><code>class="alert alert-info"</code></strong> for this.
</div>

<div class="alert alert-warning">
    This is a <strong>warning</strong> alert. Use <strong><code>class="alert alert-warning"</code></strong> for this.
</div>

<div class="alert alert-error">
    This an <strong>error</strong> alert. Use <strong><code>class="alert alert-error"</code></strong> for this.
</div>

### Auto-dismissing alerts
Alerts can be configured to dismiss automatically after a period of time by adding an attribute called <code>data-dismiss-after</code> 
to the div with a value set in milliseconds (e.g. <code>data-dismiss-after="2500"</code> dismisses the alert after 2.5 seconds).

Alerts with the <code>data-disimiss-after</code> attribute set can still be dismissed by clicking on them. 

Some examples of auto-dismissing alerts have been loaded on this page, but 
may have already been dismissed by the time you've gotten to this section of the document. Reload the page to see them in action.

<div class="alert" data-dismiss-after="1000">
    This is an alert. Use <code><strong>class="alert"</strong></code> for this. This alert will dismiss in 1 second.
</div>

<div class="alert alert-success" data-dismiss-after="2500">
    This is a <strong>success</strong> alert. Use <strong><code>class="alert alert-success"</code></strong> for this. This alert will dismiss in 2.5 seconds.
</div>

<div class="alert alert-info" data-dismiss-after="5000">
    This is an <strong>info</strong> alert. Use <strong><code>class="alert alert-info"</code></strong> for this. This alert will dismiss in 5 seconds.
</div>

<div class="alert alert-warning" data-dismiss-after="10000">
    This is a <strong>warning</strong> alert. Use <strong><code>class="alert alert-warning"</code></strong> for this. This alert will dismiss in 10 seconds.
</div>

<div class="alert alert-error" data-dismiss-after="30000">
    This an <strong>error</strong> alert. Use <strong><code>class="alert alert-error"</code></strong> for this. This alert will dismiss in 30 seconds.
</div>