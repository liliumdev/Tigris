<?php

include("config.php"); 
session_start();

if ($_SESSION["loggedin"] != 1) 
{ 
	header("Location: index.php");
	exit;
}

if(isset($_GET['allow']))
{
	$id = mysql_real_escape_string($_GET['allow']);
	
	mysql_query("UPDATE tigris_accounts SET allowed=1 WHERE id='$id'");
}

if(isset($_GET['disallow']))
{
	$id = mysql_real_escape_string($_GET['disallow']);
	
	mysql_query("UPDATE tigris_accounts SET allowed=0 WHERE id='$id'");
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
<a href="main.php"><li><b>users</b></li></a>
<a href="usage.php"><li>usage history</li></a>
<a href="usage.php?s=1"><li>suspicious</li></a>
<a href="bugs.php"><li>bugs</li></a>
</ul>
</div>

<div id="content">
<table class="bordered">
<tr>
<th>Username</th>
<th>Password</th>
<th>Hardware ID</th>
<th>First IP</th>
<th>Last IP</th>
<th>Last used</th>
<th>Allowed to use Tigris</th>
</tr>

<?
$result = mysql_query("SELECT * FROM tigris_accounts");
while($row = mysql_fetch_array($result))
{
	echo "<tr>";
	echo "<td>" . $row['username'] . "</td>";
	echo "<td>" . $row['password'] . "</td>";
	echo "<td>" . $row['hw_id'] . "</td>";
	echo "<td>" . $row['first_ip'] . "</td>";
	echo "<td>" . $row['last_ip'] . "</td>";	
	echo "<td>" . $row['last_used'] . "</td>";
	echo "<td>" . ($row['allowed'] == 0 ? "<img src='img/no.png' width='16' height='16'> No (<a href='main.php?allow=" . $row['id'] . "'>Allow</a>)" : "<img src='img/yes.png' width='16' height='16'> Yes  (<a href='?disallow=" . $row['id'] . "'>Disallow</a>)") . "</td>";
	echo "</tr>";
}

?>
</table>
</div>
</body>
</html>
