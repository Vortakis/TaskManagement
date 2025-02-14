$url = "http://localhost:5150/api/tasks"
$requests = 100000
$concurrency = 100

Start-Process -NoNewWindow -FilePath "C:\Apache24\bin\ab.exe" -ArgumentList "-n $requests -c $concurrency -p CreateTask.json -T application/json $url" -Wait
