<?php

include("config.php"); 
session_start();

if ($_SESSION["loggedin"] != 1) 
{ 
	header("Location: index.php");
	exit;
}

if(isset($_GET['view']))
{
	$id = mysql_real_escape_string($_GET['view']);
	$result = mysql_query("SELECT details FROM tigris_bugs WHERE id='$id'");
	$row = mysql_fetch_row($result);
	echo $row[0];
	exit;
}

?>

<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta http-equiv="X-UA-Compatible" content="IE=edge,chrome=1">
  <script src="http://ajax.googleapis.com/ajax/libs/jquery/1.8.0/jquery.min.js" type="text/javascript"></script>
  <script src="jquery.simplemodal.1.4.4.min.js" type="text/javascript"></script>
  <script type="text/javascript">
  function dialogmodal(bug)
  {
	  $(".details#" + bug).modal();
	  $(".details#" + bug).css('visibility', 'visible');
  }
  </script>
  <title>Tigris Authentication</title>
  <link rel="stylesheet" href="css/style.css">
  <!--[if lt IE 9]><script src="//html5shim.googlecode.com/svn/trunk/html5.js"></script><![endif]-->
</head>
<body>
<div id="navigation">
<ul>
<a href="main.php"><li>users</li></a>
<a href="usage.php"><li>usage history</li></a>
<a href="usage.php?s=1"><li>suspicious</li></a>
<a href="bugs.php"><li><b>bugs</b></li></a>
</ul>
</div>

<div id="content">
<table class="bordered">
<tr>
<th>Bug</th>
<th>Message</th>
<th>Stack trace</th>
<th>User</th>
<th>Citizen details</th>
</tr>

<?
$result = mysql_query("SELECT * FROM tigris_bugs");
while($row = mysql_fetch_array($result))
{
	echo "<tr>";
	echo "<td>" . $row['bug'] . "</td>";
	echo "<td>" . $row['message'] . "</td>";
	echo "<td><a href='#' onclick='dialogmodal(\"" . $row["id"] . "\")'>Details</a></td>";
	echo "<td>" . $row['user'] . "</td>";
	echo "<td>" . $row['citizen'] . "</td>";	
	echo "</tr>";
}
?>

</table>

<?
$result = mysql_query("SELECT id, details FROM tigris_bugs");
while($row = mysql_fetch_array($result))
{
	echo "<div class='details' id='" . $row[0] . "'>" . $row[1] . "</div>";
}

?>

</div>
</body>
</html>
