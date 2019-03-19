# SerkoTest

## Working Example

Initial .Net Core version didn't work as the PCRE lib isn't compatible with Core yet, so I ported it to Framework 4.6.x instead.

Tested with:
```
curl -X POST \
  http://localhost:5000/api/expense \
  -H 'Cache-Control: no-cache' \
  -H 'Content-Type: application/json' \
  -d '"Hi Yvaine,\nPlease create an expense claim for the below. Relevant details are marked up as requested...\n<expense><cost_centre>DEV002</cost_centre> <total>890.55</total><payment_method>personal\ncard</payment_method>\n</expense>\nFrom: Ivan Castle\nSent: Friday, 16 February 2018 10:32 AM\nTo: Antoine Lloyd <Antoine.Lloyd@example.com>\nSubject: test\nHi Antoine,\nPlease create a reservation at the <vendor>Viaduct Steakhouse</vendor> our <description>development\nteam's project end celebration dinner</description> on <date>Tuesday 27 April 2017</date>. We expect to\narrive around 7.15pm. Approximately 12 people but I'll confirm exact numbers closer to the day.\nRegards,\nIvan\n"'
```
