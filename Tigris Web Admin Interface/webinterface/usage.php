<?php

include("config.php"); 
session_start();

if ($_SESSION["loggedin"] != 1) 
{ 
	header("Location: index.php");
	exit;
}
 
?>

<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta http-equiv="X-UA-Compatible" content="IE=edge,chrome=1">
  <title>Tigris Authentication</title>
  <link rel="stylesheet" href="css/style.css">
  <!--[if lt IE 9]><script src="//html5shim.googlecode.com/svn/trunk/html5.js"></script><![endif]-->
</head>
<body>
<div id="navigation">
<ul>
<a href="main.php"><li>users</li></a>
<a href="usage.php"><li><b>usage history</b></li></a>
<a href="usage.php?s=1"><li>suspicious</li></a>
<a href="bugs.php"><li>bugs</li></a>
</ul>
</div>

<div id="content">
<table class="bordered">
<tr>
<th>Username</th>
<th>Action</th>
<th>IP</th>
<th>Hardware ID</th>
<th>Time</th>
</tr>

<?
$result = mysql_query("SELECT * FROM tigris_usages ORDER BY usage_time DESC");
while($row = mysql_fetch_array($result))
{
	echo "<tr>";
	echo "<td>" . $row['username'] . "</td>";
	echo "<td>" . $row['action'] . "</td>";
	echo "<td>" . $row['ip'] . "</td>";
	echo "<td>" . $row['hw_id'] . "</td>";
	echo "<td>" . $row['usage_time'] . "</td>";	
	echo "</tr>";
}
?>
</table>
</div>
</body>
</html>
