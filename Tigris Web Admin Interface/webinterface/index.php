<?php 
 
include("config.php");
session_start();
 
if (isset($_GET["login"])) 
{ 
	if (isset($_POST["username"]) && isset($_POST["password"]))
	{		
		$username = mysql_real_escape_string($_POST["username"]);
		$password = mysql_real_escape_string($_POST["password"]);
		$sql = "SELECT * FROM tigris_accounts WHERE username='$username' and password='$password'";
		$result = mysql_query($sql);
		
		if(mysql_num_rows($result) == 1)
		{
			$_SESSION['loggedin'] = 1;	 
			header("Location: main.php");
			exit;
		}
		else
		{
			header("Location: index.php?wrong=1");
		}
	} 
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
<section class="container">
	<div class="login">
		<h1><? if(isset($_GET['wrong'])) echo $_GET['wrong'] == 1 ? "Wrong credentials! Please try again !" : "Login to Tigris"; else echo "Login to Tigris"; ?></h1>
		<form method="post" action="?login=1">
			<p>
				<input type="text" name="username" value="" placeholder="Username">
			</p>
			<p>
				<input type="password" name="password" value="" placeholder="Password">
			</p>
			<p class="submit">
				<input type="submit" name="commit" value="Login">
			</p>
		</form>
	</div>
</section>
</body>
</html>
