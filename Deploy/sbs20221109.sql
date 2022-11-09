-- MySQL dump 10.13  Distrib 5.7.33, for Linux (x86_64)
--
-- Host: localhost    Database: smilebasicsource
-- ------------------------------------------------------
-- Server version	5.7.33-0ubuntu0.16.04.1

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `archiveviews`
--

DROP TABLE IF EXISTS `archiveviews`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `archiveviews` (
  `pagekey` varchar(127) COLLATE utf8mb4_unicode_ci NOT NULL,
  `count` bigint(20) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`pagekey`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `badgegroups`
--

DROP TABLE IF EXISTS `badgegroups`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `badgegroups` (
  `bgid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `single` tinyint(1) NOT NULL DEFAULT '0',
  `starter` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`bgid`),
  UNIQUE KEY `name` (`name`)
) ENGINE=InnoDB AUTO_INCREMENT=47 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `badges`
--

DROP TABLE IF EXISTS `badges`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `badges` (
  `bid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `file` tinytext COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `value` int(11) NOT NULL DEFAULT '0',
  `givable` tinyint(1) NOT NULL DEFAULT '0',
  `hidden` tinyint(1) NOT NULL DEFAULT '1',
  `single` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`bid`),
  UNIQUE KEY `name` (`name`)
) ENGINE=InnoDB AUTO_INCREMENT=542 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bans`
--

DROP TABLE IF EXISTS `bans`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `bans` (
  `bid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `uid` int(10) unsigned NOT NULL,
  `end` datetime NOT NULL,
  `reason` varchar(1023) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `site` tinyint(1) NOT NULL DEFAULT '0',
  `lockout` tinyint(1) NOT NULL DEFAULT '0',
  `shadow` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`bid`),
  KEY `uid` (`uid`),
  CONSTRAINT `bans_ibfk_1` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=693 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `categories`
--

DROP TABLE IF EXISTS `categories`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `categories` (
  `cid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `pcid` int(10) unsigned DEFAULT NULL,
  `name` varchar(65) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `permissions` bigint(20) unsigned NOT NULL DEFAULT '0',
  `alwaysavailable` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`cid`),
  UNIQUE KEY `name` (`name`),
  KEY `pcid` (`pcid`),
  CONSTRAINT `categories_ibfk_1` FOREIGN KEY (`pcid`) REFERENCES `categories` (`cid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=33 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `colors`
--

DROP TABLE IF EXISTS `colors`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `colors` (
  `cid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `displayname` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`cid`)
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `comments`
--

DROP TABLE IF EXISTS `comments`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `comments` (
  `cid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `pcid` int(10) unsigned DEFAULT NULL,
  `pid` int(10) unsigned NOT NULL,
  `uid` int(10) unsigned NOT NULL,
  `euid` int(10) unsigned DEFAULT NULL,
  `created` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `edited` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `content` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `status` bigint(20) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`cid`),
  KEY `pid` (`pid`),
  KEY `pcid` (`pcid`),
  KEY `uid` (`uid`),
  KEY `euid` (`euid`),
  CONSTRAINT `comments_ibfk_1` FOREIGN KEY (`pid`) REFERENCES `pages` (`pid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `comments_ibfk_2` FOREIGN KEY (`pcid`) REFERENCES `comments` (`cid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `comments_ibfk_3` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `comments_ibfk_4` FOREIGN KEY (`euid`) REFERENCES `users` (`uid`) ON DELETE NO ACTION ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=16927 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8 */ ;
/*!50003 SET character_set_results = utf8 */ ;
/*!50003 SET collation_connection  = utf8_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`root`@`localhost`*/ /*!50003 TRIGGER comments__bu BEFORE UPDATE ON comments FOR EACH ROW
INSERT INTO comments_history SELECT 'update', NULL, NOW(), d.* 
FROM comments AS d WHERE d.cid = OLD.cid */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8 */ ;
/*!50003 SET character_set_results = utf8 */ ;
/*!50003 SET collation_connection  = utf8_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`root`@`localhost`*/ /*!50003 TRIGGER comments__bd BEFORE DELETE ON comments FOR EACH ROW
INSERT INTO comments_history SELECT 'delete', NULL, NOW(), d.* 
FROM comments AS d WHERE d.cid = OLD.cid */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;

--
-- Table structure for table `comments_history`
--

DROP TABLE IF EXISTS `comments_history`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `comments_history` (
  `action` varchar(8) COLLATE utf8mb4_unicode_ci DEFAULT 'update',
  `revision` int(6) NOT NULL AUTO_INCREMENT,
  `revisiondate` datetime NOT NULL,
  `cid` int(11) unsigned NOT NULL,
  `pcid` int(10) unsigned DEFAULT NULL,
  `pid` int(10) unsigned NOT NULL,
  `uid` int(10) unsigned NOT NULL,
  `euid` int(10) unsigned DEFAULT NULL,
  `created` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `edited` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `content` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `status` bigint(20) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`cid`,`revision`),
  KEY `pid` (`pid`),
  KEY `pcid` (`pcid`),
  KEY `uid` (`uid`),
  KEY `euid` (`euid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `events`
--

DROP TABLE IF EXISTS `events`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `events` (
  `eid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `uid` int(10) unsigned NOT NULL,
  `time` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `link` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `action` varchar(63) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `area` varchar(63) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `title` varchar(1023) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `description` varchar(1023) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `hidden` tinyint(1) NOT NULL DEFAULT '0',
  `extra` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `linkid` int(10) unsigned NOT NULL,
  PRIMARY KEY (`eid`),
  KEY `uid` (`uid`),
  CONSTRAINT `events_ibfk_1` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=110052 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `forumcategories`
--

DROP TABLE IF EXISTS `forumcategories`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `forumcategories` (
  `fcid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(65) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(2001) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `permissions` bigint(20) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`fcid`),
  UNIQUE KEY `name` (`name`)
) ENGINE=InnoDB AUTO_INCREMENT=9 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `forumflags`
--

DROP TABLE IF EXISTS `forumflags`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `forumflags` (
  `fpid` int(10) unsigned NOT NULL,
  `uid` int(10) unsigned NOT NULL,
  PRIMARY KEY (`fpid`,`uid`),
  KEY `uid` (`uid`),
  CONSTRAINT `forumflags_ibfk_1` FOREIGN KEY (`fpid`) REFERENCES `forumposts` (`fpid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `forumflags_ibfk_2` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `forumposts`
--

DROP TABLE IF EXISTS `forumposts`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `forumposts` (
  `fpid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `ftid` int(10) unsigned NOT NULL,
  `uid` int(10) unsigned NOT NULL,
  `euid` int(10) unsigned DEFAULT NULL,
  `content` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `created` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `edited` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `status` bigint(20) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`fpid`),
  KEY `ftid` (`ftid`),
  KEY `uid` (`uid`),
  KEY `euid` (`euid`),
  CONSTRAINT `forumposts_ibfk_1` FOREIGN KEY (`ftid`) REFERENCES `forumthreads` (`ftid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `forumposts_ibfk_2` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `forumposts_ibfk_3` FOREIGN KEY (`euid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=32327 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8 */ ;
/*!50003 SET character_set_results = utf8 */ ;
/*!50003 SET collation_connection  = utf8_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`root`@`localhost`*/ /*!50003 TRIGGER forumposts__bu BEFORE UPDATE ON forumposts FOR EACH ROW
INSERT INTO forumposts_history SELECT 'update', NULL, NOW(), d.* 
FROM forumposts AS d WHERE d.fpid = OLD.fpid */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8 */ ;
/*!50003 SET character_set_results = utf8 */ ;
/*!50003 SET collation_connection  = utf8_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`root`@`localhost`*/ /*!50003 TRIGGER forumposts__bd BEFORE DELETE ON forumposts FOR EACH ROW
INSERT INTO forumposts_history SELECT 'delete', NULL, NOW(), d.* 
FROM forumposts AS d WHERE d.fpid = OLD.fpid */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;

--
-- Table structure for table `forumposts_history`
--

DROP TABLE IF EXISTS `forumposts_history`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `forumposts_history` (
  `action` varchar(8) COLLATE utf8mb4_unicode_ci DEFAULT 'update',
  `revision` int(6) NOT NULL AUTO_INCREMENT,
  `revisiondate` datetime NOT NULL,
  `fpid` int(11) unsigned NOT NULL,
  `ftid` int(10) unsigned NOT NULL,
  `uid` int(10) unsigned NOT NULL,
  `euid` int(10) unsigned DEFAULT NULL,
  `content` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `created` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `edited` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `status` bigint(20) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`fpid`,`revision`),
  KEY `ftid` (`ftid`),
  KEY `uid` (`uid`),
  KEY `euid` (`euid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `forumthreads`
--

DROP TABLE IF EXISTS `forumthreads`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `forumthreads` (
  `ftid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `fcid` int(10) unsigned NOT NULL,
  `uid` int(10) unsigned NOT NULL,
  `title` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `created` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `views` int(10) unsigned NOT NULL DEFAULT '0',
  `status` bigint(20) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`ftid`),
  UNIQUE KEY `title` (`title`),
  KEY `fcid` (`fcid`),
  KEY `uid` (`uid`),
  CONSTRAINT `forumthreads_ibfk_1` FOREIGN KEY (`fcid`) REFERENCES `forumcategories` (`fcid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `forumthreads_ibfk_2` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=2422 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8 */ ;
/*!50003 SET character_set_results = utf8 */ ;
/*!50003 SET collation_connection  = utf8_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`root`@`localhost`*/ /*!50003 TRIGGER forumthreads__bu BEFORE UPDATE ON forumthreads FOR EACH ROW
INSERT INTO forumthreads_history SELECT 'update', NULL, NOW(), d.* 
FROM forumthreads AS d WHERE d.ftid = OLD.ftid */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8 */ ;
/*!50003 SET character_set_results = utf8 */ ;
/*!50003 SET collation_connection  = utf8_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`root`@`localhost`*/ /*!50003 TRIGGER forumthreads__bd BEFORE DELETE ON forumthreads FOR EACH ROW
INSERT INTO forumthreads_history SELECT 'delete', NULL, NOW(), d.* 
FROM forumthreads AS d WHERE d.ftid = OLD.ftid */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;

--
-- Table structure for table `forumthreads_history`
--

DROP TABLE IF EXISTS `forumthreads_history`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `forumthreads_history` (
  `action` varchar(8) COLLATE utf8mb4_unicode_ci DEFAULT 'update',
  `revision` int(6) NOT NULL AUTO_INCREMENT,
  `revisiondate` datetime NOT NULL,
  `ftid` int(11) unsigned NOT NULL,
  `fcid` int(10) unsigned NOT NULL,
  `uid` int(10) unsigned NOT NULL,
  `title` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `created` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `views` int(10) unsigned NOT NULL DEFAULT '0',
  `status` bigint(20) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`ftid`,`revision`),
  KEY `fcid` (`fcid`),
  KEY `uid` (`uid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `givenbadges`
--

DROP TABLE IF EXISTS `givenbadges`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `givenbadges` (
  `bid` int(10) unsigned NOT NULL,
  `giver` int(10) unsigned NOT NULL,
  `receiver` int(10) unsigned NOT NULL,
  `given` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`giver`,`receiver`,`bid`),
  KEY `bid` (`bid`),
  KEY `receiver` (`receiver`),
  CONSTRAINT `givenbadges_ibfk_1` FOREIGN KEY (`bid`) REFERENCES `badges` (`bid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `givenbadges_ibfk_2` FOREIGN KEY (`receiver`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `givenbadges_ibfk_3` FOREIGN KEY (`giver`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `groupsforbadges`
--

DROP TABLE IF EXISTS `groupsforbadges`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `groupsforbadges` (
  `bid` int(10) unsigned NOT NULL,
  `bgid` int(10) unsigned NOT NULL,
  PRIMARY KEY (`bid`,`bgid`),
  KEY `bgid` (`bgid`),
  CONSTRAINT `groupsforbadges_ibfk_1` FOREIGN KEY (`bid`) REFERENCES `badges` (`bid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `groupsforbadges_ibfk_2` FOREIGN KEY (`bgid`) REFERENCES `badgegroups` (`bgid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `inspector`
--

DROP TABLE IF EXISTS `inspector`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `inspector` (
  `uid` int(10) unsigned NOT NULL,
  `ipaddress` varchar(128) COLLATE utf8mb4_unicode_ci NOT NULL,
  `lastuse` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`uid`,`ipaddress`),
  CONSTRAINT `inspector_ibfk_1` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `messagerecipients`
--

DROP TABLE IF EXISTS `messagerecipients`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `messagerecipients` (
  `mid` int(10) unsigned NOT NULL,
  `recipient` int(10) unsigned NOT NULL,
  `status` bigint(20) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`mid`,`recipient`),
  KEY `recipient` (`recipient`),
  CONSTRAINT `messagerecipients_ibfk_1` FOREIGN KEY (`mid`) REFERENCES `messages` (`mid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `messagerecipients_ibfk_2` FOREIGN KEY (`recipient`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `messages`
--

DROP TABLE IF EXISTS `messages`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `messages` (
  `mid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `sender` int(10) unsigned NOT NULL,
  `content` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `senddate` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`mid`),
  KEY `sender` (`sender`),
  CONSTRAINT `messages_ibfk_1` FOREIGN KEY (`sender`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=9417 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `notifications`
--

DROP TABLE IF EXISTS `notifications`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `notifications` (
  `nid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `uid` int(10) unsigned NOT NULL,
  `area` varchar(63) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `linkid` int(10) unsigned NOT NULL,
  `lastcheck` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`nid`),
  KEY `uid` (`uid`),
  CONSTRAINT `notifications_ibfk_1` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=85809 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ospcontest`
--

DROP TABLE IF EXISTS `ospcontest`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `ospcontest` (
  `ogid` int(10) unsigned NOT NULL,
  `isopen` tinyint(1) NOT NULL DEFAULT '0',
  `endedon` datetime NOT NULL DEFAULT '1000-01-01 00:00:00',
  `link` varchar(1023) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `uid` int(10) unsigned NOT NULL,
  PRIMARY KEY (`ogid`),
  KEY `uid` (`uid`),
  CONSTRAINT `ospcontest_ibfk_1` FOREIGN KEY (`ogid`) REFERENCES `ospgroup` (`ogid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `ospcontest_ibfk_2` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ospgroup`
--

DROP TABLE IF EXISTS `ospgroup`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `ospgroup` (
  `ogid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(127) COLLATE utf8mb4_unicode_ci NOT NULL,
  `createdon` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`ogid`),
  UNIQUE KEY `name` (`name`)
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ospsubmission`
--

DROP TABLE IF EXISTS `ospsubmission`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `ospsubmission` (
  `osid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `ogid` int(10) unsigned NOT NULL,
  `uid` int(10) unsigned NOT NULL,
  `createdon` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `codeimage` varchar(1023) COLLATE utf8mb4_unicode_ci NOT NULL,
  `runimage` varchar(1023) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `description` varchar(511) COLLATE utf8mb4_unicode_ci NOT NULL,
  `initialkey` varchar(31) COLLATE utf8mb4_unicode_ci NOT NULL,
  `filename` varchar(31) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`osid`),
  KEY `ogid` (`ogid`),
  KEY `uid` (`uid`),
  CONSTRAINT `ospsubmission_ibfk_1` FOREIGN KEY (`ogid`) REFERENCES `ospgroup` (`ogid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `ospsubmission_ibfk_2` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=46 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pageauthors`
--

DROP TABLE IF EXISTS `pageauthors`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `pageauthors` (
  `pid` int(10) unsigned NOT NULL,
  `uid` int(10) unsigned NOT NULL,
  `edit` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`pid`,`uid`),
  KEY `uid` (`uid`),
  CONSTRAINT `pageauthors_ibfk_1` FOREIGN KEY (`pid`) REFERENCES `pages` (`pid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `pageauthors_ibfk_2` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pageauthors_history`
--

DROP TABLE IF EXISTS `pageauthors_history`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `pageauthors_history` (
  `revision` int(6) NOT NULL,
  `pid` int(11) unsigned NOT NULL,
  `uid` int(11) unsigned NOT NULL,
  `edit` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`pid`,`uid`,`revision`),
  KEY `uid` (`uid`),
  KEY `fk_revision` (`revision`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pagecategories`
--

DROP TABLE IF EXISTS `pagecategories`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `pagecategories` (
  `pid` int(10) unsigned NOT NULL,
  `cid` int(10) unsigned NOT NULL,
  PRIMARY KEY (`pid`,`cid`),
  KEY `cid` (`cid`),
  CONSTRAINT `pagecategories_ibfk_1` FOREIGN KEY (`pid`) REFERENCES `pages` (`pid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `pagecategories_ibfk_2` FOREIGN KEY (`cid`) REFERENCES `categories` (`cid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pagecategories_history`
--

DROP TABLE IF EXISTS `pagecategories_history`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `pagecategories_history` (
  `revision` int(6) NOT NULL,
  `pid` int(11) unsigned NOT NULL,
  `cid` int(11) unsigned NOT NULL,
  PRIMARY KEY (`pid`,`cid`,`revision`),
  KEY `cid` (`cid`),
  KEY `fk_revision` (`revision`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pageimages`
--

DROP TABLE IF EXISTS `pageimages`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `pageimages` (
  `iid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `pid` int(10) unsigned NOT NULL,
  `uid` int(10) unsigned NOT NULL,
  `created` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `link` varchar(1024) COLLATE utf8mb4_unicode_ci NOT NULL,
  `number` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`iid`),
  KEY `pid` (`pid`),
  KEY `uid` (`uid`),
  CONSTRAINT `pageimages_ibfk_1` FOREIGN KEY (`pid`) REFERENCES `pages` (`pid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `pageimages_ibfk_2` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE NO ACTION ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=16198 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pageimages_history`
--

DROP TABLE IF EXISTS `pageimages_history`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `pageimages_history` (
  `revision` int(6) NOT NULL,
  `iid` int(11) unsigned NOT NULL,
  `pid` int(10) unsigned NOT NULL,
  `uid` int(10) unsigned NOT NULL,
  `created` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `link` varchar(1024) COLLATE utf8mb4_unicode_ci NOT NULL,
  `number` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`iid`,`revision`),
  KEY `pid` (`pid`),
  KEY `uid` (`uid`),
  KEY `fk_revision` (`revision`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pagekeywords`
--

DROP TABLE IF EXISTS `pagekeywords`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `pagekeywords` (
  `kid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `pid` int(10) unsigned NOT NULL,
  `keyword` varchar(33) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`kid`),
  KEY `pid` (`pid`),
  CONSTRAINT `pagekeywords_ibfk_1` FOREIGN KEY (`pid`) REFERENCES `pages` (`pid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=25810 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pagekeywords_history`
--

DROP TABLE IF EXISTS `pagekeywords_history`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `pagekeywords_history` (
  `revision` int(6) NOT NULL,
  `kid` int(11) unsigned NOT NULL,
  `pid` int(10) unsigned NOT NULL,
  `keyword` varchar(33) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`kid`,`revision`),
  KEY `pid` (`pid`),
  KEY `fk_revision` (`revision`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pagepromotions`
--

DROP TABLE IF EXISTS `pagepromotions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `pagepromotions` (
  `pid` int(10) unsigned NOT NULL,
  `expiration` datetime NOT NULL,
  PRIMARY KEY (`pid`),
  CONSTRAINT `pagepromotions_ibfk_1` FOREIGN KEY (`pid`) REFERENCES `pages` (`pid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pages`
--

DROP TABLE IF EXISTS `pages`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `pages` (
  `pid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `created` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `edited` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `euid` int(10) unsigned NOT NULL,
  `title` varchar(151) COLLATE utf8mb4_unicode_ci NOT NULL,
  `dlkey` varchar(33) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `version` varchar(33) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `size` varchar(33) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `body` mediumtext COLLATE utf8mb4_unicode_ci NOT NULL,
  `dmca` varchar(1024) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `support` int(10) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`pid`),
  UNIQUE KEY `title` (`title`),
  KEY `euid` (`euid`),
  CONSTRAINT `pages_ibfk_1` FOREIGN KEY (`euid`) REFERENCES `users` (`uid`) ON DELETE NO ACTION ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=1582 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8 */ ;
/*!50003 SET character_set_results = utf8 */ ;
/*!50003 SET collation_connection  = utf8_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`root`@`localhost`*/ /*!50003 TRIGGER pages__bu BEFORE UPDATE ON pages FOR EACH ROW BEGIN
   DECLARE rev INT UNSIGNED;
   SET NEW.`edited` = CURRENT_TIMESTAMP;
   INSERT INTO pages_history SELECT 'update', NULL, NOW(), d.*
      FROM pages AS d WHERE d.pid = OLD.pid;
   SET rev = LAST_INSERT_ID();
   INSERT INTO pageimages_history SELECT rev, d.* FROM pageimages AS d
      WHERE d.pid = OLD.pid;
   INSERT INTO pageauthors_history SELECT rev, d.* FROM pageauthors AS d
      WHERE d.pid = OLD.pid;
   INSERT INTO pagecategories_history SELECT rev, d.* FROM pagecategories AS d
      WHERE d.pid = OLD.pid;
   INSERT INTO pagekeywords_history SELECT rev, d.* FROM pagekeywords AS d
      WHERE d.pid = OLD.pid;
END */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8 */ ;
/*!50003 SET character_set_results = utf8 */ ;
/*!50003 SET collation_connection  = utf8_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`root`@`localhost`*/ /*!50003 TRIGGER pages__bd BEFORE DELETE ON pages FOR EACH ROW
INSERT INTO pages_history SELECT 'delete', NULL, NOW(), d.* 
FROM pages AS d WHERE d.pid = OLD.pid */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;

--
-- Table structure for table `pages_history`
--

DROP TABLE IF EXISTS `pages_history`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `pages_history` (
  `action` varchar(8) COLLATE utf8mb4_unicode_ci DEFAULT 'insert',
  `revision` int(6) NOT NULL AUTO_INCREMENT,
  `revisiondate` datetime NOT NULL,
  `pid` int(11) unsigned NOT NULL,
  `created` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `edited` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `euid` int(10) unsigned NOT NULL,
  `title` varchar(151) COLLATE utf8mb4_unicode_ci NOT NULL,
  `dlkey` varchar(33) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `version` varchar(33) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `size` varchar(33) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `body` mediumtext COLLATE utf8mb4_unicode_ci NOT NULL,
  `dmca` varchar(1024) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `support` int(10) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`pid`,`revision`),
  KEY `euid` (`euid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pagevotes`
--

DROP TABLE IF EXISTS `pagevotes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `pagevotes` (
  `pid` int(10) unsigned NOT NULL,
  `uid` int(10) unsigned NOT NULL,
  `vote` tinyint(4) NOT NULL,
  PRIMARY KEY (`pid`,`uid`),
  KEY `uid` (`uid`),
  CONSTRAINT `pagevotes_ibfk_1` FOREIGN KEY (`pid`) REFERENCES `pages` (`pid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `pagevotes_ibfk_2` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `polloptions`
--

DROP TABLE IF EXISTS `polloptions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `polloptions` (
  `poid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `pid` int(10) unsigned NOT NULL,
  `content` varchar(1023) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`poid`),
  KEY `pid` (`pid`),
  CONSTRAINT `polloptions_ibfk_1` FOREIGN KEY (`pid`) REFERENCES `polls` (`pid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=3799 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `polls`
--

DROP TABLE IF EXISTS `polls`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `polls` (
  `pid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `uid` int(10) unsigned NOT NULL,
  `title` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `closed` tinyint(1) NOT NULL DEFAULT '0',
  `hiddenresults` tinyint(1) NOT NULL DEFAULT '0',
  `created` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `link` varchar(1024) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `multivote` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`pid`),
  KEY `uid` (`uid`),
  CONSTRAINT `polls_ibfk_1` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=740 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pollvotes`
--

DROP TABLE IF EXISTS `pollvotes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `pollvotes` (
  `poid` int(10) unsigned NOT NULL,
  `uid` int(10) unsigned NOT NULL,
  `created` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`poid`,`uid`),
  KEY `uid` (`uid`),
  CONSTRAINT `pollvotes_ibfk_1` FOREIGN KEY (`poid`) REFERENCES `polloptions` (`poid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `pollvotes_ibfk_2` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `registrations`
--

DROP TABLE IF EXISTS `registrations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `registrations` (
  `uid` int(10) unsigned NOT NULL,
  `registerkey` varchar(65) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`uid`),
  CONSTRAINT `registrations_ibfk_1` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `settings`
--

DROP TABLE IF EXISTS `settings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `settings` (
  `uid` int(10) unsigned NOT NULL,
  `name` varchar(32) COLLATE utf8mb4_unicode_ci NOT NULL,
  `value` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`uid`,`name`),
  CONSTRAINT `settings_ibfk_1` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `storedvalues`
--

DROP TABLE IF EXISTS `storedvalues`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `storedvalues` (
  `name` varchar(64) COLLATE utf8mb4_unicode_ci NOT NULL,
  `value` mediumtext COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `themes`
--

DROP TABLE IF EXISTS `themes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `themes` (
  `tid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `displayname` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(4096) COLLATE utf8mb4_unicode_ci NOT NULL,
  `creator` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`tid`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `threadread`
--

DROP TABLE IF EXISTS `threadread`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `threadread` (
  `ftid` int(10) unsigned NOT NULL,
  `uid` int(10) unsigned NOT NULL,
  `time` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`ftid`,`uid`),
  KEY `uid` (`uid`),
  CONSTRAINT `threadread_ibfk_1` FOREIGN KEY (`ftid`) REFERENCES `forumthreads` (`ftid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `threadread_ibfk_2` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `userbadges`
--

DROP TABLE IF EXISTS `userbadges`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `userbadges` (
  `bid` int(10) unsigned NOT NULL,
  `uid` int(10) unsigned NOT NULL,
  `received` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `displayindex` int(11) NOT NULL DEFAULT '-1',
  PRIMARY KEY (`uid`,`bid`),
  KEY `bid` (`bid`),
  CONSTRAINT `userbadges_ibfk_1` FOREIGN KEY (`bid`) REFERENCES `badges` (`bid`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `userbadges_ibfk_2` FOREIGN KEY (`uid`) REFERENCES `users` (`uid`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `users`
--

DROP TABLE IF EXISTS `users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `users` (
  `uid` int(6) unsigned zerofill NOT NULL AUTO_INCREMENT,
  `created` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `username` varchar(21) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
  `password` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `email` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
  `authority` bigint(20) unsigned NOT NULL DEFAULT '0',
  `preferences` bigint(20) unsigned NOT NULL DEFAULT '0',
  `avatar` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'user_uploads/avatars/default.png',
  `language` varchar(16) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'en',
  `tid` int(10) unsigned NOT NULL DEFAULT '1',
  `cid` int(10) unsigned NOT NULL DEFAULT '1',
  `about` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `avatarblocked` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`uid`),
  UNIQUE KEY `username` (`username`),
  UNIQUE KEY `email` (`email`),
  KEY `tid` (`tid`),
  KEY `cid` (`cid`),
  CONSTRAINT `users_ibfk_1` FOREIGN KEY (`tid`) REFERENCES `themes` (`tid`) ON DELETE NO ACTION ON UPDATE NO ACTION,
  CONSTRAINT `users_ibfk_2` FOREIGN KEY (`cid`) REFERENCES `colors` (`cid`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB AUTO_INCREMENT=1648 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `views`
--

DROP TABLE IF EXISTS `views`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `views` (
  `pagekey` varchar(127) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ipaddress` varchar(127) COLLATE utf8mb4_unicode_ci NOT NULL,
  `time` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`pagekey`,`ipaddress`,`time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2022-11-09 20:28:04
